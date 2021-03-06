﻿//------------------------------------------------------------------------------
// <copyright file="MainWindow.xaml.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------
namespace Microsoft.Samples.Kinect.SkeletonBasics
{
    using System;
    using System.IO;
    using System.Windows;
    using System.Windows.Media;
    using System.Windows.Forms;
    using System.Diagnostics;
    using System.Threading;
    using System.Collections.Generic;
    using System.Collections;
    using Microsoft.Kinect;
    using System.Windows.Media.Imaging;
    using System.Diagnostics;
    // Zachs DMX
    using DmxComm;


    

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        
        /* ********** USER-GENERATED CONSTANTS & VARIABLES ********** */
        /**************************************************************/


        /* dmx object */
        DmxDriver dmxdev = new DmxDriver(1);
        int tempc = 0;






        /* Mode selection variables */
        enum mode { StaticMode, CaptureMode, DynamicMode }
        private mode CurrentMode = mode.CaptureMode;






        /* Saving pose variables */
        
        private int numPosesWritten = 0;
        // array holding the names of lighting effects (populates combobox in JSF)
        public String[] effectNamesArray = new String[8];
        // image of the skeleton pose (populates mainPoseImage in JSF)
        public ImageSource mainPoseImage;
        





        /* Loading pose variables */

        // skeletonPose array to hold different poses captured before a live mode
        private ArrayList poseArrayList = new ArrayList();
        // Loading text constants
        private int NAME_INDEX = 0;
        private int ANGLE_INDEX = 1;
        private int TOLERANCE_INDEX = 2;
        // loadPose counter
        private int numPosesLoaded = 0;



        


        /* Stream variables */

        // skeletonPose variable that is constantly updated directly from the skeleton stream
        private skeletonPose currentStreamPose;
        /// <summary>                                                                                                                                                
        /// Struct declaration that will house all of the joint angles and time stamp of a skeleton 
        /// *Note, neckAngle & centerShoulderAngle both use the centerShoulder as the middle 'joint'*
        /// </summary>
        struct skeletonPose
        {
            // Map normal names to joint array indices
            public enum JointLabels
            {
                rightWristAngle, rightElbowAngle, rightShoulderAngle, leftWristAngle, leftElbowAngle, leftShoulderAngle, rightAnkleAngle,
                rightKneeAngle, rightHipAngle, leftAnkleAngle, leftKneeAngle, leftHipAngle, spineAngle, neckAngle, centerShoulderAngle,
                numJoints
            };

            // Joints array that holds a pose's joint angles
            public double[] Joints;

            // Turn string name into an integer index as jointFromName["rightWristAngle"]
            public Dictionary<string, int> jointFromName;

            // Turn integer into a name as skeletonPose.Names[i]
            public string[] Names;

            // Tolerance array that holds how close a pose needs to be for each joint
            public double[] Tolerance;

            // Time variable started when pose is captured in getPose()
            public DateTime timeElapsed;

            // The desired lighting function for this pose       
            public String lightingEffectName;
        }
        // Dynamic mode sub-modes
        enum dynmodes { FOLLOW, HAND_PAN_TILT, GOBO_SPIN };
        // Start-up dynamic mode sub-mode: hand pan tilt
        private dynmodes currentDyanmicMode = dynmodes.HAND_PAN_TILT;
        // Focus value taken from the GUI slider
        double focusValue;






        /* Static function variables */

        // poseArray index that identifies a match between the stream and a saved pose
        private int matchingPoseArrayIndex;

        /* ******************** */
        /* ******************** */






        /// <summary>
        /// Width of output drawing
        /// </summary>
        private const float RenderWidth = 640.0f;

        /// <summary>
        /// Height of our output drawing
        /// </summary>
        private const float RenderHeight = 480.0f;

        /// <summary>
        /// Thickness of drawn joint lines
        /// </summary>
        private const double JointThickness = 3;

        /// <summary>
        /// Thickness of body center ellipse
        /// </summary>
        private const double BodyCenterThickness = 10;

        /// <summary>
        /// Thickness of clip edge rectangles
        /// </summary>
        private const double ClipBoundsThickness = 10;

        /// <summary>
        /// Brush used to draw skeleton center point
        /// </summary>
        private readonly Brush centerPointBrush = Brushes.Blue;

        /// <summary>
        /// Brush used for drawing joints that are currently tracked
        /// </summary>
        private readonly Brush trackedJointBrush = new SolidColorBrush(Color.FromArgb(255, 68, 192, 68));

        /// <summary>
        /// Brush used for drawing joints that are currently inferred
        /// </summary>        
        private readonly Brush inferredJointBrush = Brushes.Yellow;

        /// <summary>
        /// Pen used for drawing bones that are currently tracked
        /// </summary>
        private readonly Pen trackedBonePen = new Pen(Brushes.Green, 6);

        /// <summary>
        /// Pen used for drawing bones that are currently inferred
        /// </summary>        
        private readonly Pen inferredBonePen = new Pen(Brushes.Gray, 1);

        /// <summary>
        /// Active Kinect sensor
        /// </summary>
        private KinectSensor sensor;

        /// <summary>
        /// Drawing group for skeleton rendering output
        /// </summary>
        private DrawingGroup drawingGroup;

        /// <summary>
        /// Drawing image that we will display
        /// </summary>
        private DrawingImage imageSource;

        /// <summary>
        /// Initializes a new instance of the MainWindow class.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Draws indicators to show which edges are clipping skeleton data
        /// </summary>
        /// <param name="skeleton">skeleton to draw clipping information for</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private static void RenderClippedEdges(Skeleton skeleton, DrawingContext drawingContext)
        {
            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Bottom))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, RenderHeight - ClipBoundsThickness, RenderWidth, ClipBoundsThickness));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Top))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, RenderWidth, ClipBoundsThickness));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Left))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, ClipBoundsThickness, RenderHeight));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Right))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(RenderWidth - ClipBoundsThickness, 0, ClipBoundsThickness, RenderHeight));
            }
        }

        /// <summary>
        /// Execute startup tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void WindowLoaded(object sender, RoutedEventArgs e)
        {

            // Create the drawing group we'll use for drawing
            this.drawingGroup = new DrawingGroup();

            // Create an image source that we can use in our image control
            this.imageSource = new DrawingImage(this.drawingGroup);

            // Display the drawing using our image control
            Image.Source = this.imageSource;

            // Look through all sensors and start the first connected one.
            // This requires that a Kinect is connected at the time of app startup.
            // To make your app robust against plug/unplug, 
            // it is recommended to use KinectSensorChooser provided in Microsoft.Kinect.Toolkit
            foreach (var potentialSensor in KinectSensor.KinectSensors)
            {
                if (potentialSensor.Status == KinectStatus.Connected)
                {
                  
                    this.sensor = potentialSensor;
                    break;
                }
            }

            if (null != this.sensor)
            {
                // Turn on the skeleton stream to receive skeleton frames
                this.sensor.SkeletonStream.Enable(new TransformSmoothParameters()
                {
                    Smoothing = 0.5f,
                    Correction = 0.5f,
                    Prediction = 0.5f,
                    JitterRadius = 0.05f,
                    MaxDeviationRadius = 0.04f
                });

                // Add an event handler to be called whenever there is new color frame data
                this.sensor.SkeletonFrameReady += this.SensorSkeletonFrameReady;

                // Start the sensor!
                try
                {
                    this.sensor.Start();
                }
                catch (IOException)
                {
                    this.sensor = null;
                }
            }

            if (null == this.sensor)
            {
                this.statusBarText.Text = Properties.Resources.NoKinectReady;
            }

            effectNamesArray[0] = "rightArmStatic";
            effectNamesArray[1] = "leftArmStatic";
            effectNamesArray[2] = "lungeKneeStatic";
            effectNamesArray[3] = "vStatic";
            effectNamesArray[4] = "defaultPose";
            effectNamesArray[5] = "defaultSecondPerson";
        }

        /// <summary>
        /// Execute shutdown tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (null != this.sensor)
            {
                this.sensor.Stop();
            }
            
            // User added program shutdown
            dmxdev.stop();
            dmxdev = null;
            System.Windows.Application.Current.Shutdown();

        }

        /// <summary>
        /// Event handler for Kinect sensor's SkeletonFrameReady event
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void SensorSkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            Skeleton[] skeletons = new Skeleton[0];
            
            using (SkeletonFrame skeletonFrame = e.OpenSkeletonFrame())
            {
                if (skeletonFrame != null)
                {
                    skeletons = new Skeleton[skeletonFrame.SkeletonArrayLength];
                    skeletonFrame.CopySkeletonDataTo(skeletons);
                }
            }

            using (DrawingContext dc = this.drawingGroup.Open())
            {
                // Draw a transparent background to set the render size
                dc.DrawRectangle(Brushes.Black, null, new Rect(0.0, 0.0, RenderWidth, RenderHeight));

                if (skeletons.Length != 0)
                {
                    foreach (Skeleton skel in skeletons)
                    {
                        RenderClippedEdges(skel, dc);

                        if (skel.TrackingState == SkeletonTrackingState.Tracked)
                        {
                            this.DrawBonesAndJoints(skel, dc);


                            /***************** User-created extensions original code input *****************/
                            /*******************************************************************************/


                            // Constantly assigning the currentStreamPose variable to the current stream data (entailing joint angles and joint names)
                            currentStreamPose = getPose(skel);

                            // If the user is in capture mode: Allow capturing & loading but not pose checking or lighting effects
                            if (CurrentMode == mode.CaptureMode)
                            {
                                txtCapturedInfo.Text = "In capture mode";
                            }

                            // If the user is in static mode: Allow pose checking & lighting effects but not capturing & loading
                            else if (CurrentMode == mode.StaticMode)
                            {
                                txtCapturedInfo.Text = "In static mode";
                                // Compares if the currentStreamPose pose matches any of the saved poses in poseArray
                                matchingPoseArrayIndex = poseChecker();

                                // If one of the poses matches, activate its according dance performance function
                                if (matchingPoseArrayIndex != -99)
                                {
                                    // Lets the user know which pose the program sees as a match
                                    txtCapturedInfo.Text = "Matching Pose seen: " + ((skeletonPose)poseArrayList[matchingPoseArrayIndex]).lightingEffectName;

                                    // Passes the pose to the static lighting handler
                                    staticLightingHandler((skeletonPose)poseArrayList[matchingPoseArrayIndex]);
                                }
                            }

                            // If the user is in dynamic mode: Don't allow capturing, loading or pose checking
                            else if (CurrentMode == mode.DynamicMode)
                            {
                                // Passes the untouched stream skel skeleton to the dynamic light handler
                                dynamicModeHandler(skel);
                            }
                        }
                        /*****************/
                        /*****************/


                        else if (skel.TrackingState == SkeletonTrackingState.PositionOnly)
                        {
                            dc.DrawEllipse(
                            this.centerPointBrush,
                            null,
                            this.SkeletonPointToScreen(skel.Position),
                            BodyCenterThickness,
                            BodyCenterThickness);
                        }
                    }
                }

                // User added function for the light to pan when no one is in a frame
                else
                {

                    if (dmxdev.isEffectThreadRunning())
                    {
                        //do nothing?
                    }
                    else
                    {
                        dmxdev.setShutterOpen();
                        dmxdev.setDimmerLevel(125);
                        dmxdev.setColorContinuous(DmxDriver.color_t.BLUE_101);
                        dmxdev.threadedScanEffect(-1300, 1300, 1);
                    }
                }


                // prevent drawing outside of our render areaoa
                this.drawingGroup.ClipGeometry = new RectangleGeometry(new Rect(0.0, 0.0, RenderWidth, RenderHeight));
            }
        }

        /// <summary>
        /// Draws a skeleton's bones and joints
        /// </summary>
        /// <param name="skeleton">skeleton to draw</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private void DrawBonesAndJoints(Skeleton skeleton, DrawingContext drawingContext)
        {
            // Render Torso
            this.DrawBone(skeleton, drawingContext, JointType.Head, JointType.ShoulderCenter);
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.ShoulderLeft);
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.ShoulderRight);
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.Spine);
            this.DrawBone(skeleton, drawingContext, JointType.Spine, JointType.HipCenter);
            this.DrawBone(skeleton, drawingContext, JointType.HipCenter, JointType.HipLeft);
            this.DrawBone(skeleton, drawingContext, JointType.HipCenter, JointType.HipRight);

            // Left Arm
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderLeft, JointType.ElbowLeft);
            this.DrawBone(skeleton, drawingContext, JointType.ElbowLeft, JointType.WristLeft);
            this.DrawBone(skeleton, drawingContext, JointType.WristLeft, JointType.HandLeft);

            // Right Arm
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderRight, JointType.ElbowRight);
            this.DrawBone(skeleton, drawingContext, JointType.ElbowRight, JointType.WristRight);
            this.DrawBone(skeleton, drawingContext, JointType.WristRight, JointType.HandRight);

            // Left Leg
            this.DrawBone(skeleton, drawingContext, JointType.HipLeft, JointType.KneeLeft);
            this.DrawBone(skeleton, drawingContext, JointType.KneeLeft, JointType.AnkleLeft);
            this.DrawBone(skeleton, drawingContext, JointType.AnkleLeft, JointType.FootLeft);

            // Right Leg
            this.DrawBone(skeleton, drawingContext, JointType.HipRight, JointType.KneeRight);
            this.DrawBone(skeleton, drawingContext, JointType.KneeRight, JointType.AnkleRight);
            this.DrawBone(skeleton, drawingContext, JointType.AnkleRight, JointType.FootRight);

            // Render Joints
            foreach (Joint joint in skeleton.Joints)
            {
                Brush drawBrush = null;

                if (joint.TrackingState == JointTrackingState.Tracked)
                {
                    drawBrush = this.trackedJointBrush;
                }
                else if (joint.TrackingState == JointTrackingState.Inferred)
                {
                    drawBrush = this.inferredJointBrush;
                }

                if (drawBrush != null)
                {
                    drawingContext.DrawEllipse(drawBrush, null, this.SkeletonPointToScreen(joint.Position), JointThickness, JointThickness);
                }
            }
        }

        /// <summary>
        /// Maps a SkeletonPoint to lie within our render space and converts to Point
        /// </summary>
        /// <param name="skelpoint">point to map</param>
        /// <returns>mapped point</returns>
        private Point SkeletonPointToScreen(SkeletonPoint skelpoint)
        {
            // Convert point to depth space.  
            // We are not using depth directly, but we do want the points in our 640x480 output resolution.
            DepthImagePoint depthPoint = this.sensor.CoordinateMapper.MapSkeletonPointToDepthPoint(skelpoint, DepthImageFormat.Resolution640x480Fps30);
            return new Point(depthPoint.X, depthPoint.Y);
        }

        /// <summary>
        /// Draws a bone line between two joints
        /// </summary>
        /// <param name="skeleton">skeleton to draw bones from</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        /// <param name="jointType0">joint to start drawing from</param>
        /// <param name="jointType1">joint to end drawing at</param>
        private void DrawBone(Skeleton skeleton, DrawingContext drawingContext, JointType jointType0, JointType jointType1)
        {
            Joint joint0 = skeleton.Joints[jointType0];
            Joint joint1 = skeleton.Joints[jointType1];

            // If we can't find either of these joints, exit
            if (joint0.TrackingState == JointTrackingState.NotTracked ||
                joint1.TrackingState == JointTrackingState.NotTracked)
            {
                return;
            }

            // Don't draw if both points are inferred
            if (joint0.TrackingState == JointTrackingState.Inferred &&
                joint1.TrackingState == JointTrackingState.Inferred)
            {
                return;
            }

            // We assume all drawn bones are inferred unless BOTH joints are tracked
            Pen drawPen = this.inferredBonePen;
            if (joint0.TrackingState == JointTrackingState.Tracked && joint1.TrackingState == JointTrackingState.Tracked)
            {
                drawPen = this.trackedBonePen;
            }

            drawingContext.DrawLine(drawPen, this.SkeletonPointToScreen(joint0.Position), this.SkeletonPointToScreen(joint1.Position));
        }

        /// <summary>
        /// Handles the checking or unchecking of the seated mode combo box
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void CheckBoxSeatedModeChanged(object sender, RoutedEventArgs e)
        {
            if (null != this.sensor)
            {
                if (this.checkBoxSeatedMode.IsChecked.GetValueOrDefault())
                {
                    this.sensor.SkeletonStream.TrackingMode = SkeletonTrackingMode.Seated;
                }
                else
                {
                    this.sensor.SkeletonStream.TrackingMode = SkeletonTrackingMode.Default;
                }
            }
            Dictionary<string, int> jointFromName = new Dictionary<string, int>();
        }






        /* ********** USER-GENERATED FUNCTIONS ********** */
        /* ********************************************** */


        

        /* *****MODE SELECTION FUNCTIONS***** */

        
        /// <summary>
        /// Radio button function to select capture mode
        /// </summary>
        /// <param name="sender, e"></param>
        /// <returns> N/A </returns>
        private void captureMode_Checked(object sender, RoutedEventArgs e)
        {
            // Setting the mode option to capture
            CurrentMode = mode.CaptureMode;
            // Enabling capture function buttons
            capturePose.IsEnabled = true;
            loadPose.IsEnabled = true;
        }

        /// <summary>
        /// Radio button function to select static performance mode
        /// </summary>
        /// <param name="sender, e"></param>
        /// <returns> N/A </returns>
        private void staticMode_Checked(object sender, RoutedEventArgs e)
        {
            // If there aren't any poses in our array, don't set up live mode yet
            if (poseArrayList.Count == 0)
            {
                txtCapturedInfo.Text = "There are no loaded poses yet. Cannot start live mode.";
                CurrentMode = mode.CaptureMode;
                // Change the radio buttons
                staticMode.IsChecked = false;
                captureMode.IsChecked = true;
            }
            else
            {
                // Setting the mode option to live
                CurrentMode = mode.StaticMode;
                // Disabling capture function buttons
                capturePose.IsEnabled = false;
                loadPose.IsEnabled = false;
            }
        }

        /// <summary>
        /// Radio button function to select dynamic performance mode
        /// </summary>
        /// <param name="sender, e"></param>
        /// <returns> N/A </returns>
        private void dynamicMode_Checked(object sender, RoutedEventArgs e)
        {
            // Setting the mode option to live
            CurrentMode = mode.DynamicMode;
            // Disabling capture function buttons
            capturePose.IsEnabled = false;
            loadPose.IsEnabled = false;
        }






        /* *****WRITE POSE FUNCTIONS***** */


        /// <summary>
        /// Function that opens up the joints to check window
        /// </summary>
        /// <returns> N/A </returns>
        public void CapturePose(object sender, RoutedEventArgs e)
        {
            System.Drawing.Image convertedImage = this.ConvertElement(Image);
            convertedImage.Save("SkeletonImage.png");
            // Save off a pose from the stream
            skeletonPose capturedPose = currentStreamPose;
            // Open up the joint selection window
            JSF jointSelectionForm = new JSF();

            //jointSelectionForm.mainPoseImage = this.imageSource;

            // Send an array of lighting effect names to the joint selection form
            jointSelectionForm.effectNamesArray = this.effectNamesArray;

            // Call the joint selection form
            jointSelectionForm.ShowDialog();
            

            // Save the pose with the desired joints to check
            if(jointSelectionForm.save == true)
            {
                savePose(capturedPose, jointSelectionForm.jointTolerances, jointSelectionForm.lightingEffectName);
            }
            
        }

        /// <summary>
        /// Function that opens up the joints to check window
        /// </summary>
        /// <param name="controlToRender"></param>
        /// <returns> N/A </returns>
        private System.Drawing.Image ConvertElement(FrameworkElement controlToRender)
        {
            RenderTargetBitmap rtb = new RenderTargetBitmap(
            (int)controlToRender.ActualWidth, 
            (int)controlToRender.ActualHeight, 
            90, 
            90, 
            PixelFormats.Default);
    
            Visual vis = (Visual)controlToRender;
            rtb.Render(vis);
    
            System.Windows.Controls.Image img = 
            new System.Windows.Controls.Image();
            img.Source = rtb;
            img.Stretch = Stretch.None;
            img.Measure(new System.Windows.Size(
            (int)controlToRender.ActualWidth, 
            (int)controlToRender.ActualHeight));
            System.Windows.Size sizeImage = img.DesiredSize;
            img.Arrange(new System.Windows.Rect(new 
            System.Windows.Point(0, 0), sizeImage));
    
            RenderTargetBitmap rtb2 = new RenderTargetBitmap(
            (int)rtb.Width, 
            (int)rtb.Height, 
            90, 
            90, 
            PixelFormats.Default);
            rtb2.Render(img);
    
            PngBitmapEncoder png = new PngBitmapEncoder();
            png.Frames.Add(BitmapFrame.Create(rtb2));
    
            Stream ms = new MemoryStream();
            png.Save(ms);
    
            ms.Position = 0;
    
            System.Drawing.Image retImg = 
            System.Drawing.Image.FromStream(ms);
            return retImg;
        }

        /// <summary>
        /// Once joints to check have been selected, write the pose to text
        /// </summary>
        /// <param name="caputedPose, jointTolerances, lightingEffectName"></param>
        /// <returns> N/A </returns>
        private void savePose(skeletonPose capturedPose, double[] jointTolerances, String lightingEffectName)
        {

            // Open file dialog to pick a save location
            SaveFileDialog saveFileDiag = new SaveFileDialog();
            saveFileDiag.Filter = "Text File (*.txt)|*.txt";
            // If the file dialog works...
            if (saveFileDiag.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                // Create file writing path
                String filePath = saveFileDiag.FileName;
                // Create a new stream writer to save information from capturedPose
                StreamWriter streamWriter = new StreamWriter(filePath, true);

                // For all of our joints, write out the joint name, angle and tolerance to a line
                for (int i = 0; i < capturedPose.Joints.Length; i++)
                {
                    streamWriter.WriteLine(capturedPose.Names[i] + " " + Convert.ToString(capturedPose.Joints[i]) + " " + jointTolerances[i]);
                    
                    // The last line corresponds to the desired lighting effect
                    if ((i + 1) == capturedPose.Joints.Length)
                    {
                        streamWriter.WriteLine(lightingEffectName);
                    }
                }
                streamWriter.Close();
            }
            saveFileDiag.Dispose();

            // Increase the pose written counter
            numPosesWritten++;
        }






        /* *****LOAD POSE FUNCTIONS***** */


        /// <summary>
        /// Function to load poses from the openBox path once the user has specified a path
        /// </summary>
        /// <param name="sender, e"></param>
        /// <returns> N/A </returns>
        private void LoadPose(object sender, RoutedEventArgs e)
        {
            // Open file dialog to get the pose text to open
            OpenFileDialog openFile = new OpenFileDialog();
            openFile.Filter = "Text File (*.txt)|*.txt";
            DialogResult result = openFile.ShowDialog();
            String filePath = "";
            filePath = openFile.FileName;

            // Cancel button handler
            if(!File.Exists(filePath)){
                return;
            }
            
            // Create a stream reader to read from the newly opened file
            StreamReader streamReader = new StreamReader(filePath);

            // Creating a new pose that'll be filled with loaded pose joint angle information
            skeletonPose poseToFill = new skeletonPose();
            // Initilizing the dictionary for our new pose
            poseToFill = fillJointNames();

            // Delimiters to break up the components on each line of the pose text file
            char[] delimiters = { ' ',':' };
            // String that'll hold each line of the pose text file
            string line;

            // Read and display lines from the file until the end of the file is reached
            while ((line = streamReader.ReadLine()) != null) 
            {
                string[] words = line.Split(delimiters);
                // Checking to see if the line is our lighting effect (recognize single word line)
                if(words.Length == 1)
                {
                    poseToFill.lightingEffectName = words[0];
                }
                // Otherwise, split each line and save their values into a new pose
                else
                {
                    int whichJoint = poseToFill.jointFromName[words[NAME_INDEX]];                  // joint name = first word of line
                    poseToFill.Joints[whichJoint] = Convert.ToDouble(words[ANGLE_INDEX]);          // joint angle = second word
                    poseToFill.Tolerance[whichJoint] = Convert.ToDouble(words[TOLERANCE_INDEX]);   // joint tolerance = third word
                }
            }

            // Place the newly filled pose into the poseArray
            poseArrayList.Add(poseToFill);
            // Increase the pose loaded counter
            numPosesLoaded++;

            // User feedback once pose is loaded
            String poseLoadInfoString = "Added a new pose with effect: " + poseToFill.lightingEffectName + "\n" +
                "Total number of loaded poses: " + numPosesLoaded;
            txtCapturedInfo.Text = poseLoadInfoString;

            streamReader.Close();
            openFile.Dispose();
        }






        /* *****CONSTANT STREAM FUNCTIONS***** */


        /// <summary>
        /// Creates a new pose from the skeleton stream and fills it with joint names, captured angles, and a time stamp
        /// </summary>
        /// <param name="skel"></param>
        /// <returns>skeletonPose pose: the pose struct with the updated joint angles & time stamp</returns>
        private skeletonPose getPose(Skeleton skel)
        {
            // Creates a new blank pose
            skeletonPose pose = new skeletonPose();

            // Fills the pose with the enum joint names and initilizes Joints & fills NAMES array
            pose = fillJointNames();

            /* Right arm joint angles */
            double RWA = AngleBetweenJoints(skel.Joints[JointType.ElbowRight],
                skel.Joints[JointType.WristRight], skel.Joints[JointType.HandRight]);
            pose.Joints[(int)skeletonPose.JointLabels.rightWristAngle] = RWA;  // Determines the right wrist angle
            
            double REA = AngleBetweenJoints(skel.Joints[JointType.ShoulderRight],
                skel.Joints[JointType.ElbowRight], skel.Joints[JointType.WristRight]);
            pose.Joints[(int)skeletonPose.JointLabels.rightElbowAngle] = REA;  // Determines the right elbow angle
            
            double RSA = AngleBetweenJoints(skel.Joints[JointType.ShoulderCenter],
                skel.Joints[JointType.ShoulderRight], skel.Joints[JointType.ElbowRight]);
            pose.Joints[(int)skeletonPose.JointLabels.rightShoulderAngle] = RSA;  // Determines the right shoulder angle
            
            /* Left arm joint angles */
            double LWA = AngleBetweenJoints(skel.Joints[JointType.ElbowLeft],
                skel.Joints[JointType.WristLeft], skel.Joints[JointType.HandLeft]);
            pose.Joints[(int)skeletonPose.JointLabels.leftWristAngle] = LWA;  // Determines the left wrist angle
            double LEA = AngleBetweenJoints(skel.Joints[JointType.ShoulderLeft],
                skel.Joints[JointType.ElbowLeft], skel.Joints[JointType.WristLeft]);
            pose.Joints[(int)skeletonPose.JointLabels.leftElbowAngle] = LEA;  // Determines the left elbow angle
            double LSA = AngleBetweenJoints(skel.Joints[JointType.ShoulderCenter],
                skel.Joints[JointType.ShoulderLeft], skel.Joints[JointType.ElbowLeft]);
            pose.Joints[(int)skeletonPose.JointLabels.leftShoulderAngle] = LSA;  // Determines the left shoulder angle
            
            /* Right leg joint angles */
            double RAA = AngleBetweenJoints(skel.Joints[JointType.KneeRight],
                skel.Joints[JointType.AnkleRight], skel.Joints[JointType.FootRight]);
            pose.Joints[(int)skeletonPose.JointLabels.rightAnkleAngle] = RAA;  // Determines the right ankle angle
            double RKA = AngleBetweenJoints(skel.Joints[JointType.HipRight],
                skel.Joints[JointType.KneeRight], skel.Joints[JointType.AnkleRight]);
            pose.Joints[(int)skeletonPose.JointLabels.rightKneeAngle] = RKA;  // Determines the right knee angle
            double RHA = AngleBetweenJoints(skel.Joints[JointType.HipCenter],
                skel.Joints[JointType.HipRight], skel.Joints[JointType.KneeRight]);
            pose.Joints[(int)skeletonPose.JointLabels.rightHipAngle] = RHA;  // Determines the right hip angle
            
            /* Left leg joint angles */
            double LAA = AngleBetweenJoints(skel.Joints[JointType.KneeLeft],
                skel.Joints[JointType.AnkleLeft], skel.Joints[JointType.FootLeft]);
            pose.Joints[(int)skeletonPose.JointLabels.leftAnkleAngle] = RAA;  // Determines the left ankle angle
            double LKA = AngleBetweenJoints(skel.Joints[JointType.HipLeft],
                skel.Joints[JointType.KneeLeft], skel.Joints[JointType.AnkleLeft]);
            pose.Joints[(int)skeletonPose.JointLabels.leftKneeAngle] = LKA;  // Determines the left knee angle
            double LHA = AngleBetweenJoints(skel.Joints[JointType.HipCenter],
                skel.Joints[JointType.HipLeft], skel.Joints[JointType.KneeLeft]);
            pose.Joints[(int)skeletonPose.JointLabels.leftHipAngle] = LHA;  // Determines the left hip angle
            
            /* Torso joint angles */
            // We don't have a need for the HipCenter joint, it would a duplicate
            double SA = AngleBetweenJoints(skel.Joints[JointType.HipCenter],
                skel.Joints[JointType.Spine], skel.Joints[JointType.ShoulderCenter]);
            pose.Joints[(int)skeletonPose.JointLabels.spineAngle] = SA;  // Determines the spine angle
            double NA = AngleBetweenJoints(skel.Joints[JointType.Spine],
                skel.Joints[JointType.ShoulderCenter], skel.Joints[JointType.Head]);
            pose.Joints[(int)skeletonPose.JointLabels.neckAngle] = NA;  // Determines the neck angle *NOTE: This is the angle of the 3 vertical points for ShoulderCenter*
            double CSA = AngleBetweenJoints(skel.Joints[JointType.ShoulderLeft],
                skel.Joints[JointType.ShoulderCenter], skel.Joints[JointType.ShoulderRight]);
            pose.Joints[(int)skeletonPose.JointLabels.centerShoulderAngle] = CSA;  // Determines the neck angle *NOTE: This is the angle of the 3 horizontal points for ShoulderCenter*

            // Gets the current time of the capture; reference for next pose capture delay
            pose.timeElapsed = DateTime.Now;

            // Return the new pose with filled joint names, indicies, angles
            return pose;
        }

        /// <summary>
        /// Fills a new pose's dictionary with joint names and indicies & fills NAMES array. This is called from getPose()
        /// </summary>
        /// <returns>filledPose: pose with filled dictionary</returns>
        private skeletonPose fillJointNames()
        {
            // Creating a new skeletonPose, filling its dictionary and returning it setting it equal to pose in getPose
            skeletonPose filledPose = new skeletonPose();

            // string array constant to hold the names of the joints 
            string[] NAMES = {"rightWristAngle", "rightElbowAngle", "rightShoulderAngle", "leftWristAngle", "leftElbowAngle", "leftShoulderAngle", "rightAnkleAngle",
                "rightKneeAngle", "rightHipAngle", "leftAnkleAngle", "leftKneeAngle", "leftHipAngle", "spineAngle", "neckAngle", "centerShoulderAngle"};
            filledPose.Names = NAMES;

            // Initilizing the jointFromName dictionary 
            filledPose.jointFromName = new Dictionary<string, int>();

            // Initilize the Joints array
            filledPose.Joints = new double[(int)skeletonPose.JointLabels.numJoints];

            // Initilize the tolerance array
            filledPose.Tolerance = new double[(int)skeletonPose.JointLabels.numJoints];

            filledPose.jointFromName.Add(filledPose.Names[0], Convert.ToInt32(skeletonPose.JointLabels.rightWristAngle));
            filledPose.jointFromName.Add(filledPose.Names[1], Convert.ToInt32(skeletonPose.JointLabels.rightElbowAngle));
            filledPose.jointFromName.Add(filledPose.Names[2], Convert.ToInt32(skeletonPose.JointLabels.rightShoulderAngle));
            filledPose.jointFromName.Add(filledPose.Names[3], Convert.ToInt32(skeletonPose.JointLabels.leftWristAngle));
            filledPose.jointFromName.Add(filledPose.Names[4], Convert.ToInt32(skeletonPose.JointLabels.leftElbowAngle));
            filledPose.jointFromName.Add(filledPose.Names[5], Convert.ToInt32(skeletonPose.JointLabels.leftShoulderAngle));
            filledPose.jointFromName.Add(filledPose.Names[6], Convert.ToInt32(skeletonPose.JointLabels.rightAnkleAngle));
            filledPose.jointFromName.Add(filledPose.Names[7], Convert.ToInt32(skeletonPose.JointLabels.rightKneeAngle));
            filledPose.jointFromName.Add(filledPose.Names[8], Convert.ToInt32(skeletonPose.JointLabels.rightHipAngle));
            filledPose.jointFromName.Add(filledPose.Names[9], Convert.ToInt32(skeletonPose.JointLabels.leftAnkleAngle));
            filledPose.jointFromName.Add(filledPose.Names[10], Convert.ToInt32(skeletonPose.JointLabels.leftKneeAngle));
            filledPose.jointFromName.Add(filledPose.Names[11], Convert.ToInt32(skeletonPose.JointLabels.leftHipAngle));
            filledPose.jointFromName.Add(filledPose.Names[12], Convert.ToInt32(skeletonPose.JointLabels.spineAngle));
            filledPose.jointFromName.Add(filledPose.Names[13], Convert.ToInt32(skeletonPose.JointLabels.neckAngle));
            filledPose.jointFromName.Add(filledPose.Names[14], Convert.ToInt32(skeletonPose.JointLabels.centerShoulderAngle));
            //filledPose.jointFromName.Add(filledPose.Names[15], Convert.ToInt32(skeletonPose.JointLabels.numJoints));

            return filledPose;
        }






        /* *****POSE COMPARISON FUNCTIONS***** */


        /// <summary>
        /// Loop function that searches all of the poses in the array to see if any pose matches the currentStreamPose
        /// </summary>
        /// <returns>matchingPoseArrayIndex: index of a matching saved pose in poseArray</returns>
        private int poseChecker()
        {
            // Declare variable that will hold the index of a pose if it's a match
            int matchingPoseArrayIndex = -99;

            // for each of the poses in poseArray, see if it's a match to the currentStreamPose by checking isCurrentPose
            foreach (skeletonPose savedPose in poseArrayList)
            {
                // If the current pose in the array matches, return that index and break
                if (isPoseMatch(savedPose))
                {
                    // Save off the matching poseArray pose index
                    matchingPoseArrayIndex = poseArrayList.IndexOf(savedPose);
                    return matchingPoseArrayIndex;
                }
            }
            return matchingPoseArrayIndex;
        }

        /// <summary>
        /// Matching pose handler that checks if the passed in save pose matches the current stream pose
        /// </summary>
        /// <param name="capturedPose"></param>
        /// <param name="currentSkel"></param>
        /// <param name="jointsToCheckPoseNumber"></param>
        /// <returns>isCurrentPose: validity of comparison between the poses</returns>
        private Boolean isPoseMatch(skeletonPose savedPose)
        {
            // Setting pose match variable to true; after looking through all the desired joints and 
            // an non-matchis found, the variable will become false
            Boolean isPoseMatch = true;

            // For all of the joints, see if any aren't the same, if so break
            foreach (String jointByName in savedPose.jointFromName.Keys)
            {
                // If the particular joint is desired to be checked (there isn't a tolerance of -77 )
                if ( (savedPose.Tolerance[savedPose.jointFromName[jointByName]] != -77) )
                {
                    // If that joint is isn't within the tolerance range
                    if (!angleWithinRange(currentStreamPose.Joints[currentStreamPose.jointFromName[jointByName]], 
                                            savedPose.Joints[savedPose.jointFromName[jointByName]], 
                                            savedPose.Tolerance[savedPose.jointFromName[jointByName]]))
                    {
                        isPoseMatch = false;
                        return isPoseMatch;
                    }
                }



                //if (isJointInList(jointsToCheck, currentStreamPose) && angleWithinRange(currentStreamPose.Joints[j], savedPose.Joints[j]))
                //{
                //    isPoseMatch = true;
                //    break;
                //}
            }


            /* option 1 /* figure out how to initialize the jointFromName dictionary in a way that C#  
            for (int i = 0; i < jointsToCheck.Length; ++i)
            {
                int j = currentStreamPose.jointFromName[jointsToCheck[i]]; // joint integer from string name 
                if (!withinRange(currentStreamPose.Joints[j], capturedPose.Joints[j]))
                {
                    isCurrentPose = false;
                    break;
                }
            }
            */

            return isPoseMatch;
        }

        /// <summary>
        /// isCurrentPose helper method: 
        /// Checks the validity if the current joint angle is within +- "tolerance" of the matching saved joint angle
        /// </summary>
        /// <param name="current"></param>
        /// <param name="captured"></param>
        /// <returns>boolean value if the angle is within range</returns>
        private Boolean angleWithinRange(double currentAngle, double savedAngle, double tolerance)
        {
            if ((currentAngle >= savedAngle - tolerance) && (currentAngle <= savedAngle + tolerance))
            { return true; }
            else
            { return false; }
        }






        /* *****STATIC FUNCTIONS***** */


        /// <summary>
        /// Static lighting functions overseer & structural handler
        /// </summary>
        /// <param name="machedPose"></param>
        /// <returns>N/A</returns>
        private void staticLightingHandler(skeletonPose matchedPose)
        {
            // Static lighting effect 1
            if (matchedPose.lightingEffectName == effectNamesArray[0])
            {
                rightArmStatic();
            }
            // Static lighting effect 2
            else if (matchedPose.lightingEffectName == effectNamesArray[1])
            {
                leftArmStatic();
            }
            // Static lighting effect 3
            else if (matchedPose.lightingEffectName == effectNamesArray[2])
            {
                lungeKneeStatic();
            }
            // Static lighting effect 4
            else if (matchedPose.lightingEffectName == effectNamesArray[3])
            {
                vStatic();
            }
            // Static lighting effect 5
            else if (matchedPose.lightingEffectName == effectNamesArray[4])
            {
                defaultPose();
            }
            else if (matchedPose.lightingEffectName == effectNamesArray[5])
            {
                defaultSecondPerson();
            }

        }

        /// <summary>
        /// Ideal matched pose: Uses a thread to pan a pink light
        /// </summary>
        /// <returns> N/A </returns>
        private void rightArmStatic()
        {
            txtDynamic.Text = ("Static:  right arm static");

            resetLight();

            // Run thread effect
            if (!dmxdev.isEffectThreadRunning())
            {
                dmxdev.threadedScanEffect();
            }
            
            //rightArmStatic effects
            dmxdev.setColorContinuous(DmxDriver.color_t.PINK);
            dmxdev.setGoboStandard(5);
            dmxdev.setPrismRotate(DmxDriver.rotation_direction_t.CW, 10);
            dmxdev.setShutterOpen();
        }

        /// <summary>
        /// Ideal matched pose: Uses a thread to pan a blue light
        /// </summary>
        /// <returns> N/A </returns>
        private void leftArmStatic()
        {
            txtDynamic.Text = ("Static:  left arm static");

            resetLight();

            // Run thread effect
            if (!dmxdev.isEffectThreadRunning())
            {
                dmxdev.threadedScanEffect();
            }

            //leftArmStatic effects
            dmxdev.setColorContinuous(DmxDriver.color_t.BLUE_101);
            dmxdev.setGoboStandard(5);
            dmxdev.setPrismRotate(DmxDriver.rotation_direction_t.CW, 10);
            dmxdev.setShutterOpen();
        }

        /// <summary>
        /// Ideal matched pose: Sets green strobe on in frame main user
        /// </summary>
        /// <returns> N/A </returns>
        private void lungeKneeStatic()
        {
            txtDynamic.Text = ("Static:  lunge knee static");

            resetLight();

            //lungeKneeStatic effects
            dmxdev.setColorContinuous(DmxDriver.color_t.GREEN_202);
            dmxdev.shutterStrobe();
            dmxdev.setPan(-13);
            dmxdev.setTilt(-80);
            

        }

        /// <summary>
        /// Ideal matched pose: Sets light up with gobo effect
        /// </summary>
        /// <returns> N/A </returns>
        private void vStatic()
        {
            txtDynamic.Text = ("Static:  v static");

            resetLight();

            //vStatic effects
            dmxdev.setTilt(-70);
            dmxdev.setPan(-20);
            dmxdev.setColorContinuous(DmxDriver.color_t.RED);
            dmxdev.setGoboStandard(5);
            dmxdev.setPrismRotate(DmxDriver.rotation_direction_t.CW,10);

        }

        /// <summary>
        /// Ideal matched pose: Sets light on in frame main user
        /// </summary>
        /// <returns> N/A </returns>
        private void defaultPose()
        {
            txtDynamic.Text = ("Static:  default");

            resetLight();

            //default effects
            dmxdev.setColorContinuous(DmxDriver.color_t.WHITE);
            dmxdev.setPan(-12);
            dmxdev.setTilt(-80);

        }
        
        /// <summary>
        /// Ideal matched pose: Sets light on second, out of frame user 
        /// </summary>
        /// <returns> N/A </returns>
        private void defaultSecondPerson()
        {
            txtDynamic.Text = ("Static: default second person");
            
            resetLight();

            //default effects
            dmxdev.setColorContinuous(DmxDriver.color_t.WHITE);
            dmxdev.setPan(-20);
            dmxdev.setTilt(-82);

        }

        /// <summary>
        /// Reset functions for each time a new static effect is called 
        /// </summary>
        /// <returns> N/A </returns>
        private void resetLight()
        {
            dmxdev.endEffectThread();
            dmxdev.clearGobo();
            dmxdev.setPrismOff();
            dmxdev.setLampOn();
            dmxdev.setDimmerLevel(200);
            dmxdev.setShutterOpen();
            txtSlider.Text = Convert.ToString(focusValue);
            dmxdev.setFocus((int)focusValue);
        }






        /* *****DYNAMIC FUNCTIONS***** */


        /// <summary>
        /// Dynamic lighting functions overseer & structural handler 
        /// </summary>
        /// <param name="skel"></param>
        /// <returns> N/A </returns>
        private void dynamicModeHandler(Skeleton skel)
        {
            if (dmxdev.isEffectThreadRunning())
            {
                dmxdev.endEffectThread();
            }
            checkForModeSwitch(skel);

            switch (currentDyanmicMode)
            {
                case dynmodes.FOLLOW:
                    txtDynamic.Text = "Dynamic:  Follow";
                    dynamicFollowSkeleton(skel);
                    break;

                case dynmodes.HAND_PAN_TILT:
                    txtDynamic.Text = "Dynamic:  Hand Pan Tilt";
                    dynamicHandPanTilt(skel);
                    break;

                case dynmodes.GOBO_SPIN:
                    txtDynamic.Text = "Dynamic:  Gobo Spin";
                    dynamicGoboSpin(skel);
                    break;
            }
        }

        /// <summary>
        /// Dynamic lighting functions overseer & structural handler 
        /// </summary>
        /// <param name="skel"></param>
        /// <returns> N/A </returns>
        private void checkForModeSwitch(Skeleton skel)
        {
            // If the user holds their right hand up to their head, FF to the next sub-mode
            if (getDistanceJoints(skel.Joints[JointType.HandRight], skel.Joints[JointType.Head]) < 20)
            {
                if (stopwatch.IsRunning && stopwatch.ElapsedMilliseconds >= 2000)
                {
                    //switch mode
                    setNextDynamicMode(skel);
                    stopwatch.Stop();
                    stopwatch.Reset();
                }
                else if (stopwatch.IsRunning == false)
                {
                    stopwatch.Start();
                }

            }
            // If the user holds their left hand up to their head, RW to the previous sub-mode
            else if (getDistanceJoints(skel.Joints[JointType.HandLeft], skel.Joints[JointType.Head]) < 20)
            {
                if (stopwatch.IsRunning && stopwatch.ElapsedMilliseconds >= 2000)
                {
                    //switch mode
                    setPrevDynamicMode(skel);
                    stopwatch.Stop();
                    stopwatch.Reset();
                }
                else if (stopwatch.IsRunning == false)
                {
                    stopwatch.Start();
                }
            }
            // Otherwise, reset the stopwatch
            else if (stopwatch.IsRunning)
            {
                //detected
                stopwatch.Stop();
                stopwatch.Reset();
            }
        }

        // Dynamic mode sub-mode components
        ArrayList xList = new ArrayList();
        ArrayList yList = new ArrayList();
        Stopwatch stopwatch = new Stopwatch();

        /// <summary>
        /// Dynamic sub-mode switcher acting like a FF button 
        /// </summary>
        /// <param name="skel"></param>
        /// <returns> N/A </returns>
        private void setNextDynamicMode(Skeleton skel)
        {
            var modes = (dynmodes[])Enum.GetValues(typeof(dynmodes));

            for (int i = 0; i < modes.Length; i++)
            {
                if (modes[i] == currentDyanmicMode)
                {
                    if ((i + 1) == modes.Length)
                    {
                        currentDyanmicMode = modes[0];
                        return;
                    }
                    else
                    {
                        currentDyanmicMode = modes[i + 1];
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// Dynamic sub-mode switcher acting like a RW button 
        /// </summary>
        /// <param name="skel"></param>
        /// <returns> N/A </returns>
        private void setPrevDynamicMode(Skeleton skel)
        {
            var modes = (dynmodes[])Enum.GetValues(typeof(dynmodes));

            for (int i = 0; i < modes.Length; i++)
            {
                if (modes[i] == currentDyanmicMode)
                {
                    if (i == 0)
                    {
                        currentDyanmicMode = modes[modes.Length - 1];
                        return;
                    }
                    else
                    {
                        currentDyanmicMode = modes[i - 1];
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// Smoothing function to reduce light jitter when panning/tilting 
        /// </summary>
        /// <param name="list"></param>
        /// <returns> N/A </returns>
        private float getAverage(ArrayList list)
        {
            float sum = 0;
            foreach (float item in list)
            {
                sum += item;
            }

            return (sum / list.Count);
        }

        /// <summary>
        /// Dynamic sub-mode: Light follows user 
        /// </summary>
        /// <param name="skel"></param>
        /// <returns> N/A </returns>
        private void dynamicFollowSkeleton(Skeleton skel)
        {
            //X position of the skeleton is a -1.0 to 1.0 value with 0 being the center of the kinect screen
            // so at -1.0, we want setPan = calPanLeft, at 1.0, setPan= calPanRight, at 0 setPan = (left + right)/2.0f
           
            short centerPan = 0;
            short range = 2600;
            Stopwatch colorIterateTimer = new Stopwatch();

            // Resets
            dmxdev.setTilt(-85);
            dmxdev.setShutterOpen();
            dmxdev.setLampOn();
            dmxdev.setDimmerLevel(255);
            dmxdev.setColorContinuous(DmxDriver.color_t.WHITE);

            // Follow user
            float hipX = skel.Joints[JointType.HipCenter].Position.X;
            dmxdev.setPan16Bit((short)(centerPan + (range * hipX)));
            float rHandY = skel.Joints[JointType.HandRight].Position.Y;

            // Color selection ability
            if (rHandY > 0.5)
            {
                if (colorIterateTimer.IsRunning == false)
                {
                    colorIterateTimer.Start();
                }
                else if (colorIterateTimer.ElapsedMilliseconds > 1000)
                {
                    dmxdev.setNextColor();
                    colorIterateTimer.Stop();
                    colorIterateTimer.Reset();
                }
                if (colorIterateTimer.IsRunning == false)
                {
                    colorIterateTimer.Start();
                }
                else if (colorIterateTimer.ElapsedMilliseconds > 1000)
                {
                    dmxdev.setPrevColor();
                    colorIterateTimer.Stop();
                    colorIterateTimer.Reset();
                }
            }

        }
        
        /// <summary>
        /// Dynamic sub-mode: User controls the light's direction & brightness 
        /// </summary>
        /// <param name="skel"></param>
        /// <returns> N/A </returns>
        private void dynamicHandPanTilt(Skeleton skel)
        {

            // Resets
            dmxdev.clearGobo();
            dmxdev.setShutterOpen();
            dmxdev.setLampOn();
            dmxdev.setColorContinuous(DmxDriver.color_t.BLUE_101);

            float dimmerLevel = skel.Joints[JointType.HandLeft].Position.Y * 255;
            dmxdev.setDimmerLevel((int)dimmerLevel);
            float panPos;
            float tiltPos;

            //Attempt at smoothing our data points, average position over 5 frames before calculating a new position
            //if (xList.Count < 5)
            //{
            //    xList.Add(skel.Joints[JointType.HandRight].Position.X * 127);
            //    yList.Add(skel.Joints[JointType.HandRight].Position.Y * 127);
            //    return;
            //}
            //else
            //{
            //    panPos = getAverage(xList);
            //    tiltPos = getAverage(yList);
            //    xList.Clear();
            //    yList.Clear();
            //}

            panPos = skel.Joints[JointType.HandRight].Position.X * short.MaxValue;
            tiltPos = skel.Joints[JointType.HandRight].Position.Y * short.MaxValue;
            //double jointDistance = getDistanceJoints(skel.Joints[JointType.HandLeft], skel.Joints[JointType.HandRight]);
            //txtCapturedInfo.Text = "Left Hand X:" + (int)(skel.Joints[JointType.HandLeft].Position.X * 127);

            //dmxdev.setPan((int)panPos);
            //dmxdev.setTilt((int)tiltPos);

            dmxdev.setPan16Bit((short)panPos);
            dmxdev.setTilt16Bit((short)tiltPos);
        }
        
        /// <summary>
        /// Dynamic sub-mode: User spins gobo 
        /// </summary>
        /// <param name="skel"></param>
        /// <returns> N/A </returns>
        private void dynamicGoboSpin(Skeleton skel)
        {
            
            // Resets
            dmxdev.clearGobo();
            dmxdev.setShutterOpen();
            dmxdev.setLampOn();
            dmxdev.setGoboStandard(2);
            dmxdev.setColorContinuous(DmxDriver.color_t.GREEN_202);

            float rightHandX = skel.Joints[JointType.HandRight].Position.X * 127;
            float rightHandY = skel.Joints[JointType.HandRight].Position.Y * 127;
            dmxdev.setPan((int)rightHandX);
            dmxdev.setTilt((int)rightHandY);
            
            float goboSpin = skel.Joints[JointType.HandLeft].Position.Y * 127;
            if (goboSpin > 0)
            {
                dmxdev.setGoboSpin((DmxComm.DmxDriver.rotation_direction_t.CW),(int)goboSpin);
            }
            else if (goboSpin < 0)
            {
                dmxdev.setGoboSpin((DmxComm.DmxDriver.rotation_direction_t.CCW),(int)goboSpin);
            }
      
        }






        /* *****TEST FUNCTIONS***** */


        /// <summary>
        /// Checks to see if the current pose from the stream is in the field goal pose (both elbow joints are at a 90degree angle)
        /// </summary>
        /// <param name="capturedPose"></param>
        /// <param name="currentSkel"></param>
        /// <returns>field goal pose validity</returns>
        private Boolean isCurrentFGPose(skeletonPose capturedPose, Skeleton currentSkel)
        {
            Boolean isCurrentPose = false;
            skeletonPose currentPose = getPose(currentSkel);

            return isCurrentPose;
        }

        /// <summary>
        /// Checks to see if the current *held* pose is a match by slowly rotating the device
        /// </summary>
        /// <returns>N/A</returns>
        private void isStaticPose()
        {
            tempc += 1;
            dmxdev.setLampOn();
            dmxdev.setDimmerLevel((byte)(tempc & 0xff));
            dmxdev.setPan((byte)((tempc % 255) - 128));
            dmxdev.setTilt(110);
            dmxdev.setColorContinuous(DmxDriver.color_t.PINK);   
        }






        /* *****UTILITY FUNCTIONS***** */


        /// <summary>
        /// getPose helper Method to create original angles
        /// Return the angle between 3 Joints
        /// </summary>
        /// <param name="j1"></param>
        /// <param name="j2"></param>
        /// <param name="j3"></param>
        /// <returns>angle: the angle of the middle joint j2</returns>
        public static double AngleBetweenJoints(Joint j1, Joint j2, Joint j3)
        {
            double angle = 0;
            double shrhX = j1.Position.X - j2.Position.X;
            double shrhY = j1.Position.Y - j2.Position.Y;
            double shrhZ = j1.Position.Z - j2.Position.Z;
            double hsl = vectorNorm(shrhX, shrhY, shrhZ);
            double unrhX = j3.Position.X - j2.Position.X;
            double unrhY = j3.Position.Y - j2.Position.Y;
            double unrhZ = j3.Position.Z - j2.Position.Z;
            double hul = vectorNorm(unrhX, unrhY, unrhZ);
            double mhshu = shrhX * unrhX + shrhY * unrhY + shrhZ * unrhZ;
            double x = mhshu / (hul * hsl);
            if (x != Double.NaN)
            {
                if (-1 <= x && x <= 1)
                {
                    double angleRad = Math.Acos(x);
                    angle = angleRad * (180.0 / Math.PI);
                }
                else { angle = 0; }
            }
            else { angle = 0; }
            return angle;
        }

        /// <summary>
        /// AngleBetweenJoints helper method:
        /// Euclidean norm of 3-component Vector
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="z"></param>
        /// <returns>vector normal</returns>
        private static double vectorNorm(double x, double y, double z)
        {
            return Math.Sqrt(Math.Pow(x, 2) + Math.Pow(y, 2) + Math.Pow(z, 2));
        }

        /// <summary>
        /// Get distance between two joints
        /// Unused function: could be used with other pose match criteria
        /// </summary>
        /// <param name="joint1"></param>
        /// <param name="joint2"></param>
        /// <returns>joint distance in [*missing unit*]</returns>
        private double getDistanceJoints(Joint joint1, Joint joint2)
        {
            double x1, x2, y1, y2;

            x1 = joint1.Position.X;
            x2 = joint2.Position.X;
            y1 = joint1.Position.Y;
            y2 = joint2.Position.Y;

            double xValuesSqrd = Math.Pow((x2 - x1), 2);
            double yValuesSqrd = Math.Pow((y2 - y1), 2);

            return Math.Sqrt(xValuesSqrd + yValuesSqrd) * 100;
        }

        /// <summary>
        /// Changes the focus of the light based on the slider position
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void focusSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
           focusValue = focusSlider.Value;
           dmxdev.setFocus((int)focusValue);
           txtSlider.Text = Convert.ToString(focusValue);
        }


        /* ******************** */
        /* ******************** */
    }
}