﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.ComponentModel;
using System.Xml;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Timers;
using System.Collections.ObjectModel;

namespace EX2
{
    //////////////////////////DYNAMIC - LINKING//////////////////////////////////
    static class NativeMethods
    {
        [DllImport("kernel32.dll")]
        public static extern IntPtr LoadLibrary(string dllToLoad);

        [DllImport("kernel32.dll")]
        public static extern IntPtr GetProcAddress(IntPtr hModule, string procedureName);

        [DllImport("kernel32.dll")]
        public static extern bool FreeLibrary(IntPtr hModule);
    }

    public class FlightSimulator : IFlightSimulator
    {

        /// /////////////////////////////////external functions for TimeSeries//////////////////////////////

        /*TS*/

        //[DllImport("minCircle.dll", CallingConvention = CallingConvention.Cdecl)] //Creating pointer to anomalies TimeSeries
        //public static extern IntPtr Create_Regular_TS(String fileName, String[] atts, int size);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] //Creating pointer to anomalies TimeSeries
        private delegate IntPtr Create_Regular_TS(String fileName, String[] atts, int size);


        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] //creating a pointer for the RowSize
        private delegate IntPtr Extern_getRowSize(IntPtr ts);

        /*Data-Wrapper*/
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] //Creating a float Wrapper for a given string
        private delegate IntPtr CreateWrappedData(IntPtr ts, String s);
        //helper method
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] //given that wrapper, return it's size
        private delegate int Data_Wrapper_size(IntPtr DW);
        //helper method
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] //given that wrapper, get a value based on an index
        private delegate float Data_Wrapper_getter(IntPtr DW, int i);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] //Creates pointer to AnomalyDetector
        private delegate IntPtr CreateDetector();
        /*LearnNormal with that Detector
         writes into a txt file called 'Correlated', the correlated features
        API- "feature1" "feature2*/

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr LearnNormal(IntPtr AD, IntPtr TS);
        /*Detect using that Detector
        writes into a txt file called 'Anomalies', the anomalies from the flight
        API- "feature1" "feature2" "timestamp"*/

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr Detect(IntPtr AD, IntPtr TS);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void GetEquation(IntPtr AD);

        ///////////////////////////////////////////real content of class////////////////////////////////////

        private string[] attributesArray;

        //TimeSeries for the regular flight
        private IntPtr TS_regFlight;

        //TimeSeries for the anomaly flight
        private IntPtr TS_anomalyFlight;

        private String dllPath = Path.GetDirectoryName(System.AppDomain.CurrentDomain.BaseDirectory);//path do dll
        private IntPtr pDll; //holds address of the dll
        private IntPtr pAddressOfFunctionToCall; //holds the address of the desired function


        //AnomalyDetector
        static private IntPtr AnomalyDetector;

        // Will hold all the data of the regular flight csv. attributes are the keys
        private Dictionary<string, List<float>> regFlightDict;

        // Will hold all the data of the anomaly flight csv. attributes are the keys
        private Dictionary<string, List<float>> anomalyFlightDict;

        private Dictionary<string, string> correlatedFeatures = new Dictionary<string, string>();

        private int sliderMax = 2000;

        private string selected;

        public string Selected
        {
            get
            {
                return selected;
            }
            set
            {
                selected = value;
            }
        }

        public int SliderMax
        {
            get
            {
                return sliderMax;
            }
            set
            {
                sliderMax = value;
                NotifyPropertyChanged("SilderMax");
            }
        }

        public Dictionary<string, string> Correlated
        {
            get
            {
                return correlatedFeatures;
            }
            set
            {
                correlatedFeatures = value;
            }
        }

        // List of attributes as read from XML
        List<string> attributesList;

        IntPtr DW;
        private string FGPath;
        private ObservableCollection<KeyValuePair<float, float>> regularPoint = new ObservableCollection<KeyValuePair<float, float>>();
        private ObservableCollection<KeyValuePair<float, float>> anomalyPoint = new ObservableCollection<KeyValuePair<float, float>>();
        private ObservableCollection<KeyValuePair<float, float>> linearReg = new ObservableCollection<KeyValuePair<float, float>>();

        // how many lines are there in the received flight data csv
        private int totalLines;

        private List<string> variables = new List<string>();
        private int playbackSpeed = 10;

        private TimeSpan time;

        private System.Timers.Timer playTimer = new System.Timers.Timer();

        // A time period expressed in milliSeconds units used for playingThread.sleep(ticks)
        private int ticks;

        Thread playingThread;

        private bool stop;
        private bool pause;

        private FlightGear fg;

        // socket that is connected to the application
        private Client client;

        // This is a temp feauture untill we have the TS. Holds the data in CSV
        private List<string> dataByLines = new List<string>();

        // The last line sent to fg
        private int currentLinePlaying;

        public event PropertyChangedEventHandler PropertyChanged;


        private string pathToXML;

        private string csvData;

        public FlightSimulator()
        {
            dllPath = Directory.GetParent(dllPath).FullName;
            dllPath = Directory.GetParent(dllPath).FullName;
            dllPath += "\\Plugins\\LinearRegression.dll";
            pDll = NativeMethods.LoadLibrary(@dllPath); //load dll address

            ///now - this pDLL holds the dll's path
            ///here- load enter the desired function name:
            pAddressOfFunctionToCall = NativeMethods.GetProcAddress(pDll, "CreateDetector"); //get address of function
                                                                                             //now 'pAddressOfFunctionToCall' holds the function's address
            CreateDetector DetectorCreator = (CreateDetector)Marshal.GetDelegateForFunctionPointer(pAddressOfFunctionToCall, typeof(CreateDetector));
            AnomalyDetector = DetectorCreator();


            Console.WriteLine(dllPath);//test
            pathToXML = Path.GetDirectoryName(System.AppDomain.CurrentDomain.BaseDirectory);
            pathToXML = Directory.GetParent(pathToXML).FullName;
            pathToXML = Directory.GetParent(pathToXML).FullName;
            pathToXML += "\\resources\\playback_small.xml";
            parseXML();

            playbackSpeed = 10;
            stop = false;
            pause = false;
            // starting to play the data 10 lines in a second
            ticks = 100;
            playTimer.Interval = 100;
            CurrentLinePlaying = 0;

            client = new Client();

            playTimer.Elapsed += (s, e) =>
            {
                if (playTimer.Enabled)
                {
                    OnTimedEvent(s, e);
                }
            };
        }

        /*
       public ObservableCollection<KeyValuePair<float, float>> GetNormalPointsByFeature(string feature)
       {
           ObservableCollection<KeyValuePair<float, float>> temp = new ObservableCollection<KeyValuePair<float, float>>();

           //regularPoint.Add(new KeyValuePair<float, float>(5, 3));
           //anomalyFlightDict[feauture].GetRange(lastData, 10).ToArray();
           if (correlatedFeatures != null)
           {
               string cor = correlatedFeatures[feature];
               int lastData = CurrentLinePlaying - 300;

               if (lastData > 0)
               {
                   Dictionary<int, KeyValuePair<float, float>> good;
                   Tuple<string, string> t1 = new Tuple<string, string>(feature, cor);
                   Tuple<string, string> t2 = new Tuple<string, string>(cor, feature);
                   try
                   {
                       good = regular[t1];
                   }
                   catch (KeyNotFoundException e)
                   {
                       good = regular[t2];
                   }
                   for (int i=lastData; i < CurrentLinePlaying; i++)
                   {
                       temp
                   }

               }

           }
           return temp;
       }*/
        public void setAlgo(String path)
        {
            dllPath = path;
            pDll = NativeMethods.LoadLibrary(@dllPath); //load dll address
            ///now - this pDLL holds the dll's path
            ///here- load enter the desired function name:
            pAddressOfFunctionToCall = NativeMethods.GetProcAddress(pDll, "CreateDetector"); //get address of function
            CreateDetector DetectorCreator = (CreateDetector)Marshal.GetDelegateForFunctionPointer(pAddressOfFunctionToCall, typeof(CreateDetector));
            AnomalyDetector = DetectorCreator();
            Switched = true;
            return;
        }
        public void NotifyPropertyChanged(string propName)
        {
            if (this.PropertyChanged != null)
            {
                this.PropertyChanged(this, new PropertyChangedEventArgs(propName));
            }
            //PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }

        public Dictionary<string, List<float>> RegFlightDict
        {
            get
            {
                return regFlightDict;
            }
        }

        public int CurrentLinePlaying
        {
            get
            {
                return currentLinePlaying;
            }
            set
            {
                currentLinePlaying = value;
                NotifyPropertyChanged("CurrentLinePlaying");
            }
        }

        public Dictionary<string, List<float>> AnomalyFlightDict
        {
            get
            {
                return anomalyFlightDict;
            }
        }

        public int Playback_speed
        {
            get
            {
                return this.playbackSpeed;
            }
            set
            {
                if (value != playbackSpeed)
                {
                    if (value < 1)
                    {
                        // The minimal playbackSpeed is 1 row per second
                        this.playbackSpeed = 1;
                    }
                    else if (30 < value)
                    {
                        // The maximal playbackSpeed is 30 row per second
                        this.playbackSpeed = 30;
                    }
                    else
                    {
                        // set it to the given value
                        this.playbackSpeed = value;
                    }

                    // calculate the new ticks
                    this.ticks = 1000 / playbackSpeed;
                    playTimer.Interval = this.ticks;
                    NotifyPropertyChanged("Playback_speed");
                }
            }
        }

        public ObservableCollection<KeyValuePair<float, float>> RegularPoints
        {
            get
            {
                return regularPoint;
            }
            set
            {
                regularPoint = value;
                NotifyPropertyChanged("RegularPoints");
            }
        }

        public ObservableCollection<KeyValuePair<float, float>> AnomalyPoints
        {
            get
            {
                return anomalyPoint;
            }
            set
            {
                anomalyPoint = value;
            }
        }

        public ObservableCollection<KeyValuePair<float, float>> LinReg
        {
            get
            {
                return linearReg;
            }
            set
            {
                linearReg = value;
            }
        }

        /*
        public int CurrentLinePlaying { get; private set; }
        */

        public List<string> AttributesList
        {
            get
            {
                return this.attributesList;
            }
        }

        /*FvectorToList func.
         creates a list out of a given float-vector wrapper*/
        public List<float> FvectorToList(IntPtr DW)
        {
            ///now - this pDLL holds the dll's path
            ///here- load enter the desired function name:
            ///IntPtr pAddressOfFunctionToCall = NativeMethods.GetProcAddress(pDll, "Data_Wrapper_size"); //get address of function
            pAddressOfFunctionToCall = NativeMethods.GetProcAddress(pDll, "Data_Wrapper_size");                                                                                            //now 'pAddressOfFunctionToCall' holds the function's address
            ///now - this pDLL holds the dll's path
            ///here- load enter the desired function name:
            //get address of function
            //now 'pAddressOfFunctionToCall' holds the function's address
            Data_Wrapper_size wrapper_Size_Creator = (Data_Wrapper_size)Marshal.GetDelegateForFunctionPointer(pAddressOfFunctionToCall, typeof(Data_Wrapper_size));
            List<float> list = new List<float>();
            int size = wrapper_Size_Creator(DW);

            pAddressOfFunctionToCall = NativeMethods.GetProcAddress(pDll, "Data_Wrapper_getter");
            Data_Wrapper_getter wrapper_Size_getter_Creator = (Data_Wrapper_getter)Marshal.GetDelegateForFunctionPointer(pAddressOfFunctionToCall, typeof(Data_Wrapper_getter));

            for (int i = 0; i < size; i++)
            {
                list.Add(wrapper_Size_getter_Creator(DW, i));
            }
            return list;

        }

        private bool initialize = false;
        public bool Initialized
        {
            get
            {
                return initialize;
            } set
            {
                initialize = value;
            }
        }

        /// <summary>
        /// getVectorByName func. given a feeature, return it's values
        /// </summary>
        /// <param name="TS"></pointer to TimeSeries>
        /// <param name="name"><name of a feature>
        /// <returns></returns>
        public List<float> getVectorByName(IntPtr TS, String name)
        {
            pAddressOfFunctionToCall = NativeMethods.GetProcAddress(pDll, "CreateWrappedData");
            CreateWrappedData Wrapped_Data_Creator = (CreateWrappedData)Marshal.GetDelegateForFunctionPointer(pAddressOfFunctionToCall, typeof(CreateWrappedData));
            IntPtr DW = Wrapped_Data_Creator(TS, name); //create wrapper
            return FvectorToList(DW); //create a vector with it and send it away

        }


        public TimeSpan Time
        {
            get
            {
                return time;
            }
            private set
            {
                time = value;
                NotifyPropertyChanged("Time");
            }
        }

        private Dictionary<Tuple<string, string>, List<int>> anomalys = new Dictionary<Tuple<string, string>, List<int>>();

        public Dictionary<Tuple<string, string>, List<int>> Anomalys
        {
            get
            {
                return anomalys;
            }
        }

        private Dictionary<Tuple<string, string>, List<KeyValuePair<float, float>>> regular = new Dictionary<Tuple<string, string>, List<KeyValuePair<float, float>>>();

        public Dictionary<Tuple<string, string>, List<KeyValuePair<float, float>>> Regular
        {
            get
            {
                return regular;
            }
        }

        private Dictionary<Tuple<string, string>, Tuple<float, float>> lin_reg_eq = new Dictionary<Tuple<string, string>, Tuple<float, float>>();

        public Dictionary<Tuple<string, string>, Tuple<float, float>> Lin_reg_eq
        {
            get
            {
                return lin_reg_eq;
            } 
        }


        /// <summary>
        /// Happens each time the playTimer "ticks"
        /// </summary>
        /// <param name="source"></param>
        /// <param name="e"></param>
        private void OnTimedEvent(Object source, ElapsedEventArgs e)
        {

            Time = TimeSpan.FromMilliseconds(CurrentLinePlaying * 100);
            //Console.WriteLine(this.time);
        }

        /// <summary>
        /// Returns an array of the last 10 values of the specified feauture
        /// </summary>
        /// <param name="feauture">The feature to get data of</param>
        /// <returns>An array of floats in size 10 or less</returns>
        public float[] GetDataOfTheLastSecondByFeature(string feauture)
        {
            if (anomalyFlightDict != null)
            {
                int lastData = CurrentLinePlaying - 10;
                if (lastData > 0)
                {
                    return anomalyFlightDict[feauture].GetRange(lastData, 10).ToArray();
                }
                return anomalyFlightDict[feauture].GetRange(0, CurrentLinePlaying).ToArray();
            }
            return new float[0];
        }

        public void getAllPoints()
        {
            if (!Initialized)
            {
                return;
            }
            List<float> x = getVectorByName(TS_anomalyFlight, Selected);
            List<float> y = getVectorByName(TS_anomalyFlight, Correlated[Selected]);
            Tuple<string, string> p = new Tuple<string, string>(Selected, Correlated[Selected]);
            float x_min = x.Min();
            float x_max = x.Max();
            if (currentLinePlaying % 300 == 0 && currentLinePlaying != 0)
            {
                this.regularPoint.Clear();
                this.AnomalyPoints.Clear();
                for (int i = currentLinePlaying - 300; i < currentLinePlaying; i++)
                {
                    KeyValuePair<float, float> point = new KeyValuePair<float, float>(x[i], y[i]);
                    if (Anomalys.ContainsKey(p) && Anomalys[p].Contains(i))
                    {
                        AnomalyPoints.Add(point);
                    } else
                    {
                        RegularPoints.Add(point);
                    }
                    
                }
            }
            // add linear reg line:
            if (!Switched)
            {
                linearReg.Clear();
                if (lin_reg_eq.ContainsKey(p))
                {
                    Tuple<float, float> current = lin_reg_eq[p];
                    float a = current.Item1;
                    float b = current.Item2;
                    float y_min = a * x_min + b;
                    float y_max = a * x_max + b;
                    linearReg.Add(new KeyValuePair<float, float>(x_min, y_min));
                    linearReg.Add(new KeyValuePair<float, float>(x_max, y_max));
                }
            }
        }

        public float GetLastDataOfFeature(string feauture)
        {
            if (anomalyFlightDict != null)
            {
                return anomalyFlightDict[feauture][CurrentLinePlaying];
            }
            return 0;
        }

        public void StopPlayback()
        {
            stop = true;
            playTimer.Stop();
            CurrentLinePlaying = 0;
        }

        public void PausePlayback()
        {
            pause = true;
            this.ticks = Timeout.Infinite;
            playTimer.Stop();
        }

        private bool switched = false;
        public bool Switched
        {
            get
            {
                return switched;
            } set
            {
                switched = true;
            }
        }

        private void parse_correlatedFeatures()
        {
            string filePath = Path.GetDirectoryName(System.AppDomain.CurrentDomain.BaseDirectory);
            filePath += "\\Correlated.txt";
            string[] lines = System.IO.File.ReadAllLines(filePath);
            List<string> first = new List<string>();
            List<string> second = new List<string>();
            foreach (string line in lines)
            {
                string[] vars = line.Split(' ');
                Correlated.Add(vars[0], vars[1]);
                first.Add(vars[0]);
                second.Add(vars[1]);
            }

            filePath = Path.GetDirectoryName(System.AppDomain.CurrentDomain.BaseDirectory);
            filePath += "\\Equation.txt";
            string[] lines2 = System.IO.File.ReadAllLines(filePath);
            if (!Switched)
            {
                int idx = 0;
                foreach (string line in lines2)
                {
                    string[] vars = line.Split(',');
                    if (vars[0] != "-nan(ind)")
                    {
                        float a = float.Parse(vars[0], CultureInfo.InvariantCulture.NumberFormat);
                        float b = float.Parse(vars[1], CultureInfo.InvariantCulture.NumberFormat);
                        Tuple<string, string> current = new Tuple<string, string>(first[idx], second[idx]);
                        lin_reg_eq[current] = new Tuple<float, float>(a, b);
                    }
                    idx++;

                }
            }

        }

        // Holds the path to regular flight CSV file
        private string regFlightCSV;

        public string RegFlightCSV
        {
            get
            {
                return regFlightCSV;
            }
            set
            {
                if (this.regFlightCSV != value)
                {
                    pAddressOfFunctionToCall = NativeMethods.GetProcAddress(pDll, "Create_Regular_TS"); //get address of function
                    Create_Regular_TS TS_Creator = (Create_Regular_TS)Marshal.GetDelegateForFunctionPointer(pAddressOfFunctionToCall, typeof(Create_Regular_TS));
                    this.regFlightCSV = value;
                    TS_regFlight = TS_Creator(this.regFlightCSV, attributesArray, attributesArray.Length);
                    regFlightDict = new Dictionary<string, List<float>>();
                    foreach (var item in attributesArray)
                    {
                        regFlightDict.Add(item, getVectorByName(TS_regFlight, item));
                    }
                    pAddressOfFunctionToCall = NativeMethods.GetProcAddress(pDll, "LearnNormal");
                    LearnNormal LearnNormal_Creator = (LearnNormal)Marshal.GetDelegateForFunctionPointer(pAddressOfFunctionToCall, typeof(LearnNormal));
                    LearnNormal_Creator(AnomalyDetector, TS_regFlight);
                    pAddressOfFunctionToCall = NativeMethods.GetProcAddress(pDll, "GetEquation");
                    GetEquation Equation_creator = (GetEquation)Marshal.GetDelegateForFunctionPointer(pAddressOfFunctionToCall, typeof(GetEquation));
                    Equation_creator(AnomalyDetector);
                    parse_correlatedFeatures();
                    Initialized = true;
                }
            }
        }

        // Holds the path to regular flight CSV file
        private String anomalyFlightCSV;

        public string AnomalyFlightCSV
        {
            get
            {
                return anomalyFlightCSV;
            }
            set
            {
                if (this.anomalyFlightCSV != value)
                {
                    this.anomalyFlightCSV = value;
                    readCSV(value);
                    ///now - this pDLL holds the dll's path
                    ///here- load enter the desired function name:
                    pAddressOfFunctionToCall = NativeMethods.GetProcAddress(pDll, "Create_Regular_TS"); //get address of function
                    //now 'pAddressOfFunctionToCall' holds the function's address
                    Create_Regular_TS TS_Creator = (Create_Regular_TS)Marshal.GetDelegateForFunctionPointer(pAddressOfFunctionToCall, typeof(Create_Regular_TS));
                    TS_anomalyFlight = TS_Creator(this.anomalyFlightCSV, attributesArray, attributesArray.Length);
                    anomalyFlightDict = new Dictionary<string, List<float>>();
                    foreach (var item in attributesArray)
                    {
                        anomalyFlightDict.Add(item, getVectorByName(TS_anomalyFlight, item));
                    }
                    pAddressOfFunctionToCall = NativeMethods.GetProcAddress(pDll, "Detect");
                    Detect Detect_Creator = (Detect)Marshal.GetDelegateForFunctionPointer(pAddressOfFunctionToCall, typeof(Detect));
                    Detect_Creator(AnomalyDetector, TS_anomalyFlight);
                    parseAnomalys();
                }
            }
        }

        private float a;
        private float b;

        public float A
        {
            get
            {
                return a;
            } set
            {
                a = value;
            }
        }

        public float B
        {
            get
            {
                return b;
            } set
            {
                b = value;
            }
        }

        public void parseAnomalys()
        {
            string filePath = Path.GetDirectoryName(System.AppDomain.CurrentDomain.BaseDirectory);
            filePath += "\\Anomalies.txt";
            string[] lines = System.IO.File.ReadAllLines(filePath);
            foreach (string line in lines)
            {
                string[] vars = line.Split(' ');
                string f1 = vars[0];
                string f2 = vars[1];
                int line_num = Int32.Parse(vars[2]);
                Tuple<string, string> p = new Tuple<string, string>(f1, f2);
                if (Anomalys.ContainsKey(p))
                {
                    Anomalys[p].Add(line_num);

                } else
                {
                    Anomalys.Add(p, new List<int>());
                    Anomalys[p].Add(line_num);
                }
            }
        }


        /*
        public void setCSVFile(string name)
        {
            this.csvData = name;
            readCSV(this.csvData);
        }
        */
        public void setFGPath(string name)
        {
            this.FGPath = name;
            this.fg = new FlightGear(name, pathToXML);
            this.fg.openApplication();

        }

        public void play()
        {
            if (!pause && !stop)
            {
                // meaning it's the 1st time we play data to fg
                // so we need to establish connection
                this.client.connect("127.0.0.1", 5400);
                Console.WriteLine("connected");
            }
            else if (pause)
            {
                // Meaning the playingThread is sleeping infinite time and we need to resume it
                pause = false;
                stop = false;
                this.ticks = 1000 / playbackSpeed;
                this.playingThread.Interrupt();
                playTimer.Start();
                return;
            }
            else if (stop)
            {
                Console.WriteLine("in the play method in the if stop = true");
                stop = false;
                pause = false;
            }

            playTimer.Start();
            //  we now initialize a new thread
            this.playingThread = new Thread(new ThreadStart(this.playback));
            this.playingThread.Start();
        }

        /// <summary>
        /// This is the function that runs in a different thread and sends data to fg
        /// </summary>
        private void playback()
        {
            try
            {
                String line;
                while (!this.stop)
                {
                    try
                    {
                        line = getLine(this.CurrentLinePlaying);
                        line += "\r\n";
                        client.write(line);

                        Thread.Sleep(ticks);
                        CurrentLinePlaying++;

                        if (CurrentLinePlaying == dataByLines.Count)
                        {
                            StopPlayback();
                        }
                    }
                    catch (ThreadInterruptedException e)
                    {
                        // meaning the thread noe needs to continue after it's been paused
                        continue;
                    }
                }
                Console.WriteLine("The thread has stopped!");
            }
            catch (ArgumentNullException e)
            {
                Console.WriteLine("ArgumentNullException: {0}", e);
            }
            catch (SocketException e)
            {
                Console.WriteLine("SocketException: {0}", e);
            }
            catch (Exception e)
            {
                Console.WriteLine("SocketException: {0}", e);
            }
        }

        /// <summary>
        /// Temp function untill we have ts, gets the specific line
        /// </summary>
        /// <param name="lineNumber">the line number to get</param>
        /// <returns></returns>
        public String getLine(int lineNumber)
        {
            if (lineNumber < this.dataByLines.Count)
            {
                return this.dataByLines[lineNumber];
            }
            return "error in getLine";
        }

        /// <summary>
        /// Returns the number of samples received in the CSV aka - number of lines
        /// </summary>
        /// <returns>Number of samples in the CSV</returns>
        public int NumberOfFlighSamples()
        {
            return dataByLines.Count;
        }

        /// <summary>
        /// Temp function untill we have ts, gets the specific line
        /// </summary>
        /// <param name="csvPath"></param>
        private void readCSV(string path)
        {
            Console.WriteLine("readsthe CSV and saves lines");
            using (StreamReader rd = new StreamReader(path))
            {
                String line;

                while ((line = rd.ReadLine()) != null)
                {
                    this.dataByLines.Add(line);
                }
            }
        }


        public void CloseAll()
        {
            if (this.playingThread != null)
            {
                this.playingThread.Abort();
            }
        }


        /// <summary>
        /// This methods gets the feautures stated in the XML file
        /// </summary>
        public void parseXML()
        {
            attributesList = new List<string>();

            XmlReaderSettings settings = new XmlReaderSettings();
            settings.IgnoreWhitespace = true;
            XmlReader reader = null;

            /*
               string filePath = Path.GetDirectoryName(System.AppDomain.CurrentDomain.BaseDirectory);
               filePath = Directory.GetParent(filePath).FullName;
               filePath = Directory.GetParent(filePath).FullName;
               reader = XmlReader.Create(filePath + "\\resources\\playback_small.xml", settings);
               */


            // TODO read from XML the speed and save it as a property.
            string att;
            reader = XmlReader.Create(pathToXML, settings);
            if (reader.ReadToFollowing("output"))
            {
                reader.Read();
                while (reader.Name != "output")
                {
                    if (reader.IsStartElement())
                    {

                        if (reader.Name == "chunk")
                        {
                            reader.Read();
                            att = reader.ReadString();
                            if (attributesList.Contains(att) == true)
                            {
                                att += "1";
                            }
                            attributesList.Add(att);
                        }
                    }
                    reader.Read();
                }
            }


            attributesArray = attributesList.ToArray();
            if (reader != null)
                reader.Close();

        }
        /// <summary>
        /// User selected new variable to focus on.
        /// </summary>
        /// <param name="name"></param>
        public void variableSelected(string name)
        {
            Console.WriteLine(name);
            return;
        }
    }
}

