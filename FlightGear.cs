﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;

namespace EX2
{
    /// <summary>
    /// This class responsible for all the flight gear part
    /// </summary>
    class FlightGear
    {
        // path to the application file
        private string fileName;

        //path to the main directory
        private string workingDirectory;

        /*
        // socket that is connected to the application
        private Client client;
        */

        // command line arguments to pass to the application
        private string arguments = "--generic=socket,in,10,127.0.0.1,5400,tcp,playback_small --fdm=null";

        public FlightGear(string fileName, string settings)
        {
            //client = new Client();

            workingDirectory = Directory.GetParent(fileName).FullName;
            workingDirectory = Directory.GetParent(workingDirectory).FullName;
            this.fileName = fileName;
            
            string fileToCopy = settings;
        
            var pathToProtocol = workingDirectory + "\\data\\Protocol\\";


            var destination = Path.Combine(pathToProtocol, Path.GetFileName(fileToCopy));


            try
            {
                // Will not overwrite if the destination file already exists.
                File.Copy(fileToCopy, destination);
            }

            // Catch exception if the file was already copied.
            catch (IOException copyError)
            {
                Console.WriteLine(copyError.Message);
            }
            
            
            //arguments = arguments + settings + " --fdm=null";

        }

        /// <summary>
        /// opens the flight gear application
        /// </summary>
        public void openApplication()
        {
            ProcessStartInfo startInfo = new ProcessStartInfo(fileName);

            startInfo.WorkingDirectory = this.workingDirectory+"\\bin";

            startInfo.Arguments = arguments;

            Process.Start(startInfo);
        }

        /*
        public void sendData(string data)
        {
            
            if (!client.connectionEstablished)
            {
                client.connect("127.0.0.1", 5400);
            }
            
            client.write(data+ "\\r\\n");
        }*/
    }
}
