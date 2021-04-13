﻿using System.Collections.Generic;
using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Win32;
using System.ComponentModel;
using Helpers.MVVM;
using System.Timers;
using System.Globalization;

namespace EX2
{
    public partial class ViewModel : INotifyPropertyChanged
    {



        //INotifyPropertyChanged 
        public event PropertyChangedEventHandler PropertyChanged;

        public void OnPropertyChanged(string propertyName)
        {
            if (this.PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }

        System.Timers.Timer playTimer = new System.Timers.Timer();
        //responsible for the amount of lines read per second.
        private const int BUFFER_SIZE = 10;
        private const int BASE_FRAMERATE_PER_SECOND = 10;
        private FlightSimulator sim;



        private FrameStack model;
        private FrameStack Model 
        { 
            get
            {
                return model;
            }
            set
            {
                model = value;
                OnPropertyChanged(nameof(Model));
                OnPropertyChanged(nameof(FrameStackProperties));
                OnPropertyChanged(nameof(SliderMaximum));
            }
        }

        public List<string> FrameStackProperties
        {
            get
            {
                return Model?.Properties ?? new List<string>();
            }
        }
        //resposible for the slider's value.
        public int SliderMaximum
        {
            get
            {
                return (model?.Count - 1 - BUFFER_SIZE) ?? 0;
            }
        }

        private object selectedItem;
        public object SelectedProperty
        {
            get
            {
                return selectedItem;
            }
            set
            {
                selectedItem = (string)value;
                OnPropertyChanged(nameof(SelectedProperty));
                DataPropertyChanged();
            }
        }

        private int currentFrame;
        public int CurrentFrame
        {
            get
            {
                return currentFrame;
            }
            set
            {
                currentFrame = value;
                OnPropertyChanged(nameof(CurrentFrame));
                OnPropertyChanged(nameof(CurrentTime));
                DataPropertyChanged();
            }
        }

        //here  we bind all the features(graphs,clocks etc),
        //and they are being notified when certain change occured.
        private void DataPropertyChanged()
        {
            OnPropertyChanged(nameof(CurrentDataSet));
            OnPropertyChanged(nameof(TopGraphImageSource));
            OnPropertyChanged(nameof(TopGraphImageSource2));
            OnPropertyChanged(nameof(BottomGraphImageSource));
            OnPropertyChanged(nameof(AileronProperty));
            OnPropertyChanged(nameof(ElevatorPropery));
            OnPropertyChanged(nameof(Throttle));
            OnPropertyChanged(nameof(Rudder));

            OnPropertyChanged(nameof(Altimeter));
            OnPropertyChanged(nameof(Airspeed));
            OnPropertyChanged(nameof(FlightDirection));
            OnPropertyChanged(nameof(Pitch));
            OnPropertyChanged(nameof(Yaw));
        }
        //the current 10 lines or whatever value we chose.
        public float[] CurrentDataSet
        {
            get
            {
                try
                {
                    return model[(string)SelectedProperty].Skip(currentFrame + 1).Take(BUFFER_SIZE).ToArray();
                }
                catch
                {
                    return new float[0];
                }
            }
        }

        private double speedMultiplier = 1;
        public double SpeedMultiplier
        {
            get
            {
                return speedMultiplier;
            }
            set
            {
                if(Math.Round(value, 6) > 0)
                {
                    speedMultiplier = value;
                    UpdateTimerInterval();
                    OnPropertyChanged(nameof(SpeedMultiplier));
                    OnPropertyChanged(nameof(CurrentFrameRate));
                    OnPropertyChanged(nameof(CurrentTime));
                }
            }
        }

        public double CurrentFrameRate
        {
            get
            {
                return BASE_FRAMERATE_PER_SECOND * SpeedMultiplier;
            }
        }
        //gives us the time of the current value if the time.
        public TimeSpan CurrentTime
        {
            get
            {
                return TimeSpan.FromMilliseconds(CurrentFrame * playTimer.Interval);
            }
        }

        #region BindedProperties
 //top first graph on the left which shows the information. based on user pick. 
        public DrawingImage TopGraphImageSource
        {
            get
            {
                var ds = CurrentDataSet;
                return new DrawingImage(GetGraphGroup(ds, false, 10, 10, ds.Length > 0 ? (int)Math.Ceiling(Math.Max(Math.Abs(CurrentDataSet.Min()), Math.Abs(CurrentDataSet.Max()))) : 1));
            }
        }

        //top second  graph on the left which shows the information. should be based on correlation.
        public DrawingImage TopGraphImageSource2
        {
            get
            {
                var ds = CurrentDataSet;

                return new DrawingImage(GetGraphGroup(ds, false, 10, 10, ds.Length > 0 ? (int)Math.Ceiling(Math.Max(Math.Abs(CurrentDataSet.Min()), Math.Abs(CurrentDataSet.Max()))) : 1));
            }
        }
        //bottom graph.
        public DrawingImage BottomGraphImageSource
        {
            get
            {
                var ds = CurrentDataSet;

                return new DrawingImage(GetGraphGroup(ds, true, 20, 10, ds.Length > 0 ? (int)Math.Ceiling(Math.Max(Math.Abs(CurrentDataSet.Min()), Math.Abs(CurrentDataSet.Max()))) : 1));
            }
        }

        public double AileronProperty => model["aileron"].Skip(currentFrame + 1).Take(BUFFER_SIZE).First();
        public double ElevatorPropery => model["elevator"].Skip(currentFrame + 1).Take(BUFFER_SIZE).First();

        public double Throttle => model["throttle"].Skip(currentFrame + 1).Take(BUFFER_SIZE).First();

        public double Rudder => model["rudder"].Skip(currentFrame + 1).Take(BUFFER_SIZE).First();
        public double RudderMin = 0;
        public double RudderMax = 1;

       
        public double Altimeter => model["altimeter_indicated-altitude-ft"].Skip(currentFrame + 1).Take(BUFFER_SIZE).First();
        public double AltimeterMin = -100;
        public double AltimeterMax = 1000;

        public double Airspeed => model["airspeed-indicator_indicated-speed-kt"].Skip(currentFrame + 1).Take(BUFFER_SIZE).First();
        public double AirspeedMin = 0;
        public double AirspeedMax => model["airspeed-indicator_indicated-speed-kt"].Max();

        public double FlightDirection => model["indicated-heading-deg"].Skip(currentFrame + 1).Take(BUFFER_SIZE).First();
        public double FlightDirectionMin => model["indicated-heading-deg"].Min();
        public double FlightDirectionMax => model["indicated-heading-deg"].Max();

        public double Pitch => model["pitch-deg"].Skip(currentFrame + 1).Take(BUFFER_SIZE).First();
        public double PitchMin => model["pitch-deg"].Min();
        public double PitchMax => model["pitch-deg"].Max();

        public double Yaw => model["side-slip-deg"].Skip(currentFrame + 1).Take(BUFFER_SIZE).First();
        public double YawMin => model["side-slip-deg"].Min();
        public double YawMax => model["side-slip-deg"].Max();

        #endregion

        public RelayCommand StopCommand { get; private set; } 

        public RelayCommand PauseCommand { get; private set; } 

        public RelayCommand ChangeSpeedCommand { get; private set; }

        public ViewModel(FlightSimulator sim)
        {
            //Test version of model. Just for testing
            this.sim = sim;
            Model = new FrameStack(sim);

            ChangeSpeedCommand = new RelayCommand((s) =>
            {
                var deltaSpeedMultiplier = Convert.ToDouble(s, System.Globalization.CultureInfo.InvariantCulture);
                //Change timer speed only on new frame start not to make "double resetting" if it is running now

                if(playTimer.Enabled)
                {
                    ElapsedEventHandler dlg = null;
                    dlg = (s1, e1) =>
                    {
                        SpeedMultiplier += deltaSpeedMultiplier;
                        playTimer.Elapsed -= dlg;
                    };
                    playTimer.Elapsed += dlg;
                }
                else
                {
                    SpeedMultiplier += deltaSpeedMultiplier;
                }

            });

            PauseCommand = new RelayCommand((s) =>
            {
                //not used at the moment.
                if(SelectedProperty == null)
                {
                    MessageBox.Show("Chose the property in the listbox to show");
                    return;
                }

                var newStateIsPlaying = Convert.ToBoolean(s);

                if(newStateIsPlaying)
                {
                    if (CurrentFrame == SliderMaximum)
                    {
                        CurrentFrame = 0;
                    }

                    playTimer.Start();
                }
                else
                {
                    playTimer.Stop();
                }
            });

            StopCommand = new RelayCommand((s) =>
            {
                playTimer.Stop();
                CurrentFrame = 0;
            });

            UpdateTimerInterval();
            playTimer.Elapsed += (s, e) =>
            {
                try
                {
                    //Executing this in main (UI) thread because otherwise it will not push UI to update
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        //If timer is still enabled (fixes behavior when elapsed task appears in threadpool earlier when stop-command)
                        if (playTimer.Enabled)
                        {
                            if (CurrentFrame != SliderMaximum)
                            {
                                CurrentFrame++;
                            }
                            else
                            {
                                playTimer.Stop();
                            }
                        }
                    });
                }
                catch
                {

                }
            };
        }

        private void UpdateTimerInterval()
        {
            playTimer.Interval = 1000 / CurrentFrameRate;
        }
        private DrawingGroup GetGraphGroup(float[] Data, bool dots, int width = 10, int height = 10, int maximum = 5)
        {
            DrawingGroup aDrawingGroup = new DrawingGroup();
            int Np = Data.Length - 1;


            for (int DrawingStage = 0; DrawingStage < 10; DrawingStage++)
            {
                GeometryDrawing drw = new GeometryDrawing();
                GeometryGroup gg = new GeometryGroup();


                //Background
                if (DrawingStage == 1)
                {
                    drw.Brush = Brushes.Beige;
                    drw.Pen = new Pen(Brushes.LightGray, 0.01);

                    RectangleGeometry myRectGeometry = new RectangleGeometry();
                    myRectGeometry.Rect = new Rect(0, 0, width, height);
                    gg.Children.Add(myRectGeometry);
                }

                if (DrawingStage == 3)
                {
                    if(!dots)
                    {
                        drw.Brush = Brushes.White;
                        drw.Pen = new Pen(Brushes.Black, 0.05);

                        gg = new GeometryGroup();
                        for (int i = 0; i < Np; i++)
                        {
                            LineGeometry l = new LineGeometry(new Point((double)(i * width) / Np, height / 2d - Data[i] / ((double)maximum * 2 / height)),
                                new Point((double)(i + 1) * width / Np, height / 2 - (Data[i + 1]) / ((double)maximum * 2 / height)));
                            gg.Children.Add(l);
                        }
                    }
                    else
                    {
                        drw.Brush = Brushes.Black;
                        drw.Pen = new Pen(Brushes.Black, 0.05);

                        gg = new GeometryGroup();
                        for (int i = 0; i < Np; i++)
                        {
                            EllipseGeometry el = new EllipseGeometry(new Point((double)(i * width) / Np, height / 2 - Data[i] / ((double)maximum * 2 / height)), 0.01 * Math.Min(width, height), 0.01 * Math.Min(width, height));
                            gg.Children.Add(el);
                        }
                    }

                }

                //Cutting
                if (DrawingStage == 5)
                {
                    drw.Brush = Brushes.Transparent;
                    drw.Pen = new Pen(Brushes.White, 0.2);

                    RectangleGeometry myRectGeometry = new RectangleGeometry();
                    myRectGeometry.Rect = new Rect(-0.1 * width, -0.1 * height, 1.2 * width, 1.2 * height);
                    gg.Children.Add(myRectGeometry);
                }


                //border-מסגרת.
                if (DrawingStage == 6)
                {
                    drw.Brush = Brushes.Transparent;
                    drw.Pen = new Pen(Brushes.LightGray, 0.01);

                    RectangleGeometry myRectGeometry = new RectangleGeometry();
                    myRectGeometry.Rect = new Rect(0 * width, 0 * height, 1 * width, 1 * height);
                    gg.Children.Add(myRectGeometry);
                }


                //labels
                if (DrawingStage == 7)
                {
                    drw.Brush = Brushes.LightGray;
                    drw.Pen = new Pen(Brushes.Gray, 0.003);

                    for (int i = 0; i < 11; i++)
                    {
                        // Create a formatted text string.
#pragma warning disable CS0618 
                        FormattedText formattedText = new FormattedText(
                            ((double)(maximum - i * 0.1 * maximum * 2)).ToString(),
                            CultureInfo.GetCultureInfo("en-us"),
                            FlowDirection.LeftToRight,
                            new Typeface("Verdana"),
                            0.05 * height,
                            Brushes.Black);
#pragma warning restore CS0618 

                        // Set the font weight to Bold for the formatted text.
                        formattedText.SetFontWeight(FontWeights.Bold);

                        // Build a geometry out of the formatted text.
                        Geometry geometry = formattedText.BuildGeometry(new Point(-0.1 * width, i * 0.1 * height - 0.03 * height));
                        gg.Children.Add(geometry);
                    }
                }

                drw.Geometry = gg;
                aDrawingGroup.Children.Add(drw);
            }

            return aDrawingGroup;
        }


        public void set_train_csv(string name)
        {
            sim.RegFlightCSV = name;
            update_data();

        }

        public void update_data()
        {
            Dictionary<string, List<float>> data = Model.Frames;
            Dictionary<string, List<float>> frames = sim.RegFlightDict;
            foreach (string var in sim.Variables)
            {
                data[var] = frames[var];
            }

        }
        public void set_test_csv(string name)
        {
             sim.AnomalyFlightCSV = name; //PRODUCES RUNTIME ERROR !!!
        }

        public void set_flight_gear(string name)
        {
            sim.setFGPath(name);
        }

        public void change_speed(int value)
        {
            sim.Playback_speed += value;
            
        }

        /// <summary>
        /// This button plays the playback
        /// </summary>
        public void play()
        {
            sim.play();
        }

        /// <summary>
        /// This button pauses the playback
        /// </summary>
        public void stop()
        {
            sim.Pause = true;
        }

        /// <summary>
        /// This button stops the playback and next time it will play from the start
        /// </summary>
        public void restart()
        {            
            sim.Stop = true;
        }

    }



    //general format of accepting information to be presented.
    class FrameStack
    {
        private Dictionary<string, List<float>> frames;


        public Dictionary<string, List<float>> Frames
        {
            get
            {
                return frames;
            }
            set
            {
                Frames = value;
            }
        }
        public List<string> Properties
        {
            get
            {
                return frames.Keys.ToList();
            }
        }

        public List<float> this[string propertyName]
        {
            get
            {
                return frames[propertyName];
            }
        }

        public int Count
        {
            get
            {
                return frames.First().Value.Count;
            }
        }

        public FrameStack(FlightSimulator sim)
        {
            //altimeter,airspeed,flight direction,pitch,yaw,roll
            //TEST DATA INJECTING
            
            frames = new Dictionary<string, List<float>>();
            
            foreach (string var in sim.Variables)
            {
                frames.Add(var, new List<float>());
                for (int i = 0; i < 10; i++) {
                    frames[var].Add(0);
                }

            }
        }
    }
}