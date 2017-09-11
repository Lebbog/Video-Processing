//---------------------------------------------------------------------------
// ResultofSearch.cs
// Geoffrey Lebbos CSS 490 B
// 6/02/2017
// Last Modified: 6/4/2017
//---------------------------------------------------------------------------
using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using AForge.Video.FFMPEG;

namespace Assignment4V3
{
    public partial class ResultofSearch : Form
    {
        DisplayForm video;          //For displaying a sequence of images as a video
        Form1 originalForm;     
        int[,] intensityMatrix;     //To hold intensity histogram values for each frame
        double[] frameDifference;   //To store the manhatten distance between a frame and the one following
        int[] cuts;                 //To keep track of the frames where the camera cut
        int cutIndex = 0;           //For keeping track of number of cuts
        int columnNum = 0;          //Keep track of which picture
        string videoTitle = "";
        string videoPath = "";
        // Thresholds
        double cutThresh, gradThresh;
        //int Tor = 2;
        int page;                   //Keep track of what page the user is on
        int totalPages;             //Based on the number of cuts/transitions
        List<Bitmap> list;          //To store the bitmaps of the cut/transition frames
        List<int> frameList;        //To store the frame number of cuts/transitions
        int transitionIndex = 0;
        struct transitionIndeces    //Keep track of gradual transition start/end
        {
            public int start;
            public int end;
        }
        transitionIndeces[] transitions;    //Array of structs to keep track of transition start/end
        public ResultofSearch(Form1 form)
        {
            originalForm = form;
            InitializeComponent();
            intensityMatrix = new int[4000, 26];    //To store frame intensity values and resolution at 25
            frameDifference = new double[3999];     //To store the SDi of each frame
            cuts = new int[100];                    //default to 100 cuts
            for (int i = 0; i < cuts.Length; i++)
            {
                cuts[i] = int.MaxValue;             //Defualt to intmax
            }
            transitions = new transitionIndeces[100]; //default to 100 for now
            for (int i = 0; i < cuts.Length; i++)
            {
                transitions[i].start = int.MaxValue;  //Defualt to intmax
                transitions[i].end = int.MaxValue;    //Defualt to intmax
            }
            list = new List<Bitmap>();
        }

        /*Process videos displays the starting frame of each shot
        based on the twin-comparison approach*/
        public void processVideo(string videoName, string path)
        {
            videoTitle = videoName; //store name of video
            videoPath = path;       //store path of video
            if (!File.Exists("difference.txt"))
            {
                VideoFileReader videoReader = new VideoFileReader();
                videoReader.Open(videoName); //Open video

                for (int i = 0; i < 5000; i++)    //Using 5000 because .FrameCount may return 0 on accident
                {
                    Bitmap currentFrame = videoReader.ReadVideoFrame();
                    if (i >= 1000 && i <= 4999) //Only care about frames 1000-4999
                    {
                        //Record intensity value of current frame in intensityMatrix
                        intensity(currentFrame);
                    }
                    else
                    {
                        currentFrame.Dispose();
                    }
                    //Don't care about the rest of the frames
                    if (i > 4999)
                    {
                        videoReader.Close();
                        break;
                    }
                }
            }
            /*At this point, intensityMatrix contains intensity values of frames 1000-4999,
              Now I have to calculate the difference between each frame.*/
            calculateDifference();

            //After calculating difference, set thresholds
            setThreshHolds();

            //After setting thresholds, loop and find cuts/transitions
            findCutsandTrans();

            //Populate list with correct first frames
            populateList();

            //Set page numbers
            double numberPerPage = (list.Count / 20.0);
            //Round up just incase number isn't even
            totalPages = (int)Math.Ceiling(numberPerPage);
            pageLabel.Text = "Page 1 /Out of " + totalPages;
            page = 0;

            //Display results
            displayResults();
        }
        /*Intensity calculates the intensity values of the current frame
        and stores the values in intensityMatrix*/
        private void intensity(Bitmap currentFrame)
        {
            //Acquire dimensions of currentFrame
            int width = currentFrame.Width;
            int height = currentFrame.Height;
            int res = width * height;
            Color pixCol;
            int r, g, b;
            double intensity;

            for (int i = 0; i < width; i++)
            {
                for (int j = 0; j < height; j++)
                {

                    pixCol = currentFrame.GetPixel(i, j);
                    r = pixCol.R;
                    g = pixCol.G;
                    b = pixCol.B;

                    //Calculate intensity based on equation
                    intensity = (0.299 * r) + (0.587 * g) + (0.114 * b);

                    //Increment bin based on intensity/10
                    if (intensity < 250)
                    {
                        intensityMatrix[columnNum, (int)intensity / 10]++;
                    }
                    //Special case for anything above 250
                    else
                    {
                        intensityMatrix[columnNum, 24]++;
                    }
                    intensityMatrix[columnNum, intensityMatrix.GetLength(1) - 1] = res; //Slot[25] holds resolution of image
                }
            }
            columnNum++; //Increment for next picture
        }
        //calculateDifference will fill an array (frameDifference) with the difference between each frame
        private void calculateDifference()
        {
            if (!File.Exists("difference.txt"))
            {
                double difference = 0; //used to store difference between frames
                for (int i = 0; i < intensityMatrix.GetLength(0); i++)
                {
                    if (i != intensityMatrix.GetLength(0) - 1) //No frame after 4,999
                    {
                        for (int j = 0; j < intensityMatrix.GetLength(1) - 1; j++)
                        {
                            difference += Math.Abs((intensityMatrix[i, j] / (double)intensityMatrix[i, intensityMatrix.GetLength(1) - 1])
                                - (intensityMatrix[i + 1, j]) / 
                                (double)intensityMatrix[i + 1, intensityMatrix.GetLength(1) - 1]);  // Divide by resolution and subtract values for difference
                        }
                        frameDifference[i] = difference;
                        difference = 0.0; //Reset difference to 0
                    }
                }

                diffToFile();   //write difference values to file
            }
            else //Text file with difference values exists
            {
                //Read values from text file and parse as doubles
                string[] differenceValues;
                string line;
                System.IO.StreamReader differenceFile =
            new System.IO.StreamReader("difference.txt");
                line = differenceFile.ReadLine();
                differenceValues = line.Split(',');
                for (int i = 0; i < frameDifference.Length; i++)
                {
                    frameDifference[i] = double.Parse(differenceValues[i]);
                }
                //close file
                differenceFile.Close();
            }
        }
        //Used to set threshholds for twin-comparison approach
        private void setThreshHolds()
        {
            List<double> columnList = new List<double>();
            for (int i = 0; i < frameDifference.Length; i++)
            {
                columnList.Add(frameDifference[i]);
            }
            //Send list to calculate average/standard deviation
            CalculateStdDev(columnList);

        }
        private void CalculateStdDev(IEnumerable<double> values)
        {
            //acquire average and standard deviation
            double avg = values.Average();
            double stDev = Math.Sqrt(values.Average(v => Math.Pow(v - avg, 2)));

            //Now set thresholds
            cutThresh = avg + (stDev * 11);  //Threshold for cut
            gradThresh = avg * 2;            //Threshold for possible gradual transition
        }
        //Find cuts and transitions in frameDifference
        private void findCutsandTrans()
        {
            int potentialStart = 0, potentialEnd = 0;
            for (int i = 0; i < frameDifference.Length; i++)
            {
                //Check for cut
                if (frameDifference[i] >= cutThresh)
                {
                    cuts[cutIndex] = i + 1000 + 1; //Store cut as a video frame number 
                    cutIndex++;                    //increment cut index
                }
                else if (frameDifference[i] >= gradThresh)
                {
                    potentialStart = i;           //Store index as a potential start
                    //enter sub loop to check 
                    for (int j = potentialStart; j < frameDifference.Length; j++)
                    {
                        if (j + 1 < frameDifference.Length - 1 && frameDifference[j + 1] >= cutThresh)
                        {
                            cuts[cutIndex] = j + 1 + 1000 + 1; //Store cut as a video frame number 
                            cutIndex++;                        //increment cut index
                            potentialEnd = j;
                            i = potentialEnd + 1;
                            break;
                        }
                        //Check if the next two frames are below gradThresh
                        else if (j + 2 <= frameDifference.Length - 2 && frameDifference[j + 1] < gradThresh && frameDifference[j + 2] < gradThresh)
                        {
                            potentialEnd = j;
                            i = potentialEnd + 1;            //Start i from this frame
                            break;
                        }
                    }
                    //Evaluate start/end
                    evaluateTransition(potentialStart, potentialEnd);
                }
            }
        }
        //Function to evaluate a potential tansition
        private void evaluateTransition(int start, int end)
        {
            double Transitionsum = 0;
            for (int i = start; i <= end; i++)
            {
                Transitionsum += frameDifference[i];          //add values of potential start/end
            }
            if (Transitionsum >= cutThresh) //Found a gradual transition, store values of end/start
            {
                transitions[transitionIndex].start = start + 1000 + 1;
                transitions[transitionIndex].end = end + 1000 + 1;
                transitionIndex++;
            }
        }
        //To write difference values to a text file for faster processing
        private void diffToFile()
        {
            string line = "";
            for (int i = 0; i < frameDifference.Length; i++)
            {
                line += frameDifference[i].ToString();
                line += ",";
            }
            addToFile(line, "difference.txt");
        }
        //Add lines to a file
        public void addToFile(string line, string file)
        {
            FileStream fileWriter = new FileStream(file, FileMode.Append);
            StreamWriter tw = new StreamWriter(fileWriter);
            tw.WriteLine(line);
            tw.Close();
            fileWriter.Close();
        }
        //Previous page click
        private void button1_Click_1(object sender, EventArgs e)
        {
            panel1.VerticalScroll.Value = 0;
            panel1.VerticalScroll.Value = 0;
            panel1.HorizontalScroll.Value = 0; //assign the position value    
            panel1.HorizontalScroll.Value = 0;

            //Display proper page number
            if (page < (totalPages - 1))
            {
                page += 1;
                pageLabel.Text = "Page " + (page + 1) + "/Out of " + totalPages;
                displayResults();
            }
            else
            {
                page = 0;
                pageLabel.Text = "Page " + (page + 1) + "/Out of " + totalPages;
                displayResults();
            }
        }
        //Next page click
        private void button2_Click_1(object sender, EventArgs e)
        {
            panel1.VerticalScroll.Value = 0;
            panel1.VerticalScroll.Value = 0;
            panel1.HorizontalScroll.Value = 0; //assign the position value    
            panel1.HorizontalScroll.Value = 0;
            //Display proper page number
            if (page > 0)
            {
                page -= 1;
                pageLabel.Text = "Page " + (page + 1) + "/Out of " + totalPages;
                displayResults();
            }
            else
            {
                page = (totalPages - 1);
                pageLabel.Text = "Page " + (page + 1) + "/Out of " + totalPages;
                displayResults();
            }
        }
        //Method used to sort frame numbers and capture frames to put in list
        private void populateList()
        {
            List<int> toSort = new List<int>(); //Fixed value of 100          
            for (int i = 0; i < transitions.Length; i++)
            {
                if (transitions[i].start != int.MaxValue)
                {
                    toSort.Add(transitions[i].start);  //Fill list with frame number of transitions
                }
                else
                {
                    break;
                }
            }
            for (int i = 0; i < cuts.Length; i++)
            {
                if (cuts[i] != int.MaxValue)
                {
                    toSort.Add(cuts[i]);             //Fill list with frame number of cuts
                }
                else
                {
                    break;
                }
            }
            toSort.Add(1000); //Add starting frame
            //toSort now contains frame numbers of cuts/transitions, have to sort
            //Sort list
            toSort.Sort();
            //Copy frames
            frameList = toSort;

            //Add starting frames of cuts/transition in order to list
            VideoFileReader videoReader = new VideoFileReader();
            videoReader.Open(videoTitle);
            for (int i = 0; i < 5000; i++)
            {
                Bitmap currentFrame = videoReader.ReadVideoFrame();
                if (toSort.Contains(i))
                {
                    list.Add(currentFrame);
                }
                else
                {
                    currentFrame.Dispose();
                }
            }
            videoReader.Close();
        }
        //Close form and end process on X button
        private void ResultofSearch_FormClosing(object sender, FormClosingEventArgs e)
        {
            originalForm.Close();
        }
        //displayResults will fill the pictureboxes with frames based on page offset
        public void displayResults()
        {
            int offSet = page * 20;

            //for the first picturebox, grab the first element + offset in the list and display it.
            if (0 + offSet < list.Count)
            {
                Bitmap image1 = list[0];
                Image newImag1 = image1.GetThumbnailImage(150, 150, new System.Drawing.Image.GetThumbnailImageAbort(ThumbnailCallback), IntPtr.Zero);
                pictureBox1.Image = newImag1;
            }
            else
            {
                pictureBox1.Image = null;
            }
            //Same code as above except grab second element from the list
            //All the code below is the same except for the index number, so I won't comment
            if (1 + offSet < list.Count)
            {
                Bitmap image1 = list[1 + offSet];
                Image newImag1 = image1.GetThumbnailImage(150, 150, new System.Drawing.Image.GetThumbnailImageAbort(ThumbnailCallback), IntPtr.Zero);
                pictureBox2.Image = newImag1;
            }
            else
            {
                pictureBox2.Image = null;
            }

            if (2 + offSet < list.Count)
            {
                Bitmap image1 = list[2 + offSet];
                Image newImag1 = image1.GetThumbnailImage(150, 150, new System.Drawing.Image.GetThumbnailImageAbort(ThumbnailCallback), IntPtr.Zero);
                pictureBox3.Image = newImag1;
            }
            else
            {
                pictureBox3.Image = null;
            }


            if (3 + offSet < list.Count)
            {
                Bitmap image1 = list[3 + offSet];
                Image newImag1 = image1.GetThumbnailImage(150, 150, new System.Drawing.Image.GetThumbnailImageAbort(ThumbnailCallback), IntPtr.Zero);
                pictureBox4.Image = newImag1;
            }
            else
            {
                pictureBox4.Image = null;
            }

            if (4 + offSet < list.Count)
            {
                Bitmap image1 = list[4 + offSet];
                Image newImag1 = image1.GetThumbnailImage(150, 150, new System.Drawing.Image.GetThumbnailImageAbort(ThumbnailCallback), IntPtr.Zero);
                pictureBox5.Image = newImag1;
            }
            else
            {
                pictureBox5.Image = null;
            }

            if (5 + offSet < list.Count)
            {
                Bitmap image1 = list[5 + offSet];
                Image newImag1 = image1.GetThumbnailImage(150, 150, new System.Drawing.Image.GetThumbnailImageAbort(ThumbnailCallback), IntPtr.Zero);
                pictureBox6.Image = newImag1;
            }
            else
            {
                pictureBox6.Image = null;
            }

            if (6 + offSet < list.Count)
            {
                Bitmap image1 = list[6 + offSet];
                Image newImag1 = image1.GetThumbnailImage(150, 150, new System.Drawing.Image.GetThumbnailImageAbort(ThumbnailCallback), IntPtr.Zero);
                pictureBox7.Image = newImag1;
            }
            else
            {
                pictureBox7.Image = null;
            }

            if (7 + offSet < list.Count)
            {
                Bitmap image1 = list[7 + offSet];
                Image newImag1 = image1.GetThumbnailImage(150, 150, new System.Drawing.Image.GetThumbnailImageAbort(ThumbnailCallback), IntPtr.Zero);
                pictureBox8.Image = newImag1;
            }
            else
            {
                pictureBox8.Image = null;
            }
            if (8 + offSet < list.Count)
            {
                Bitmap image1 = list[8 + offSet];
                Image newImag1 = image1.GetThumbnailImage(150, 150, new System.Drawing.Image.GetThumbnailImageAbort(ThumbnailCallback), IntPtr.Zero);
                pictureBox9.Image = newImag1;
            }
            else
            {
                pictureBox9.Image = null;
            }

            if (9 + offSet < list.Count)
            {
                Bitmap image1 = list[9 + offSet];
                Image newImag1 = image1.GetThumbnailImage(150, 150, new System.Drawing.Image.GetThumbnailImageAbort(ThumbnailCallback), IntPtr.Zero);
                pictureBox10.Image = newImag1;
            }
            else
            {
                pictureBox10.Image = null;
            }

            if (10 + offSet < list.Count)
            {
                Bitmap image1 = list[10 + offSet];
                Image newImag1 = image1.GetThumbnailImage(150, 150, new System.Drawing.Image.GetThumbnailImageAbort(ThumbnailCallback), IntPtr.Zero);
                pictureBox11.Image = newImag1;
            }

            else
            {
                pictureBox11.Image = null;
            }
            if (11 + offSet < list.Count)
            {
                Bitmap image1 = list[11 + offSet];
                Image newImag1 = image1.GetThumbnailImage(150, 150, new System.Drawing.Image.GetThumbnailImageAbort(ThumbnailCallback), IntPtr.Zero);
                pictureBox12.Image = newImag1;
            }

            else
            {
                pictureBox12.Image = null;
            }

            if (12 + offSet < list.Count)
            {
                Bitmap image1 = list[12 + offSet];
                Image newImag1 = image1.GetThumbnailImage(150, 150, new System.Drawing.Image.GetThumbnailImageAbort(ThumbnailCallback), IntPtr.Zero);
                pictureBox13.Image = newImag1;
            }

            else
            {
                pictureBox13.Image = null;
            }

            if (13 + offSet < list.Count)
            {
                Bitmap image1 = list[13 + offSet];
                Image newImag1 = image1.GetThumbnailImage(150, 150, new System.Drawing.Image.GetThumbnailImageAbort(ThumbnailCallback), IntPtr.Zero);
                pictureBox14.Image = newImag1;
            }

            else
            {
                pictureBox14.Image = null;
            }

            if (14 + offSet < list.Count)
            {
                Bitmap image1 = list[14 + offSet];
                Image newImag1 = image1.GetThumbnailImage(150, 150, new System.Drawing.Image.GetThumbnailImageAbort(ThumbnailCallback), IntPtr.Zero);
                pictureBox15.Image = newImag1;
            }

            else
            {
                pictureBox15.Image = null;
            }

            if (15 + offSet < list.Count)
            {
                Bitmap image1 = list[15 + offSet];
                Image newImag1 = image1.GetThumbnailImage(150, 150, new System.Drawing.Image.GetThumbnailImageAbort(ThumbnailCallback), IntPtr.Zero);
                pictureBox16.Image = newImag1;
            }

            else
            {
                pictureBox16.Image = null;
            }

            if (16 + offSet < list.Count)
            {
                Bitmap image1 = list[16 + offSet];
                Image newImag1 = image1.GetThumbnailImage(150, 150, new System.Drawing.Image.GetThumbnailImageAbort(ThumbnailCallback), IntPtr.Zero);
                pictureBox17.Image = newImag1;
            }

            else
            {
                pictureBox17.Image = null;
            }

            if (17 + offSet < list.Count)
            {
                Bitmap image1 = list[17 + offSet];
                Image newImag1 = image1.GetThumbnailImage(150, 150, new System.Drawing.Image.GetThumbnailImageAbort(ThumbnailCallback), IntPtr.Zero);
                pictureBox18.Image = newImag1;
            }

            else
            {
                pictureBox18.Image = null;
            }

            if (18 + offSet < list.Count)
            {
                Bitmap image1 = list[18 + offSet];
                Image newImag1 = image1.GetThumbnailImage(150, 150, new System.Drawing.Image.GetThumbnailImageAbort(ThumbnailCallback), IntPtr.Zero);
                pictureBox19.Image = newImag1;
            }

            else
            {
                pictureBox19.Image = null;
            }

            if (19 + offSet < list.Count)
            {
                Bitmap image1 = list[19 + offSet];
                Image newImag1 = image1.GetThumbnailImage(150, 150, new System.Drawing.Image.GetThumbnailImageAbort(ThumbnailCallback), IntPtr.Zero);
                pictureBox20.Image = newImag1;
            }
            else
            {
                pictureBox20.Image = null;
            }
        }

        //Picturebox clicks
        private void pictureBox1_Click(object sender, EventArgs e)
        {
            if (pictureBox1.Image != null)
            {
                int offSet = page * 20;
                int startingFrame = frameList[0 + offSet] + 1; //acquire starting frame
                if (0 + offSet + 1 < frameList.Count)          //Check if next frame is ending frame
                {
                    int endingFrame = frameList[0 + offSet + 1];
                    Bitmap[] currentFrames = new Bitmap[endingFrame - startingFrame]; //Make bitmap array with the size of frame difference
                    int framesIndex = 0;
                    VideoFileReader videoReader = new VideoFileReader();
                    videoReader.Open(videoTitle);

                    for (int i = 0; i < 5000; i++)
                    {
                        Bitmap currentFrame = videoReader.ReadVideoFrame();  //Read video frame
                        if (i >= startingFrame && i < endingFrame)
                        {
                            currentFrames[framesIndex++] = currentFrame;    //If in range of starting-ending, add to array
                        }
                        else
                        {
                            currentFrame.Dispose();    //else dispose
                        }
                        if (i >= endingFrame)
                        {
                            break;        //Break if we reach ending frame
                        }
                    }
                    videoReader.Close();
                    video = new DisplayForm(currentFrames); //Send in array to DisplayForm 
                    video.Show();                           //Show video
                }
                else
                {
                    playEndingFrame();
                }
            }
        }
        //Code for all the boxes below are the same as the above box, except different indeces, doesn't need commenting
        private void pictureBox2_Click(object sender, EventArgs e)
        {
            if (pictureBox2.Image != null)
            {
                int offSet = page * 20;
                int startingFrame = frameList[1 + offSet] + 1; //acquire starting frame
                if (1 + offSet + 1 < frameList.Count)          //Check if next frame is ending frame
                {
                    int endingFrame = frameList[1 + offSet + 1];
                    Bitmap[] currentFrames = new Bitmap[endingFrame - startingFrame];
                    int framesIndex = 0;
                    VideoFileReader videoReader = new VideoFileReader();
                    videoReader.Open(videoTitle);

                    for (int i = 0; i < 5000; i++)
                    {
                        Bitmap currentFrame = videoReader.ReadVideoFrame();
                        if (i >= startingFrame && i < endingFrame)
                        {
                            currentFrames[framesIndex++] = currentFrame;
                        }
                        else
                        {
                            currentFrame.Dispose();
                        }
                        if (i >= endingFrame)
                        {
                            break;
                        }
                    }
                    videoReader.Close();
                    video = new DisplayForm(currentFrames);
                    video.Show();
                }
                else
                {
                    playEndingFrame();
                }
            }
        }
        private void pictureBox3_Click(object sender, EventArgs e)
        {
            if (pictureBox3.Image != null)
            {
                int offSet = page * 20;
                int startingFrame = frameList[2 + offSet] + 1; //acquire starting frame
                if (2 + offSet + 1 < frameList.Count)          //Check if next frame is ending frame
                {
                    int endingFrame = frameList[2 + offSet + 1];
                    Bitmap[] currentFrames = new Bitmap[endingFrame - startingFrame];
                    int framesIndex = 0;
                    VideoFileReader videoReader = new VideoFileReader();
                    videoReader.Open(videoTitle);

                    for (int i = 0; i < 5000; i++)
                    {
                        Bitmap currentFrame = videoReader.ReadVideoFrame();
                        if (i >= startingFrame && i < endingFrame)
                        {
                            currentFrames[framesIndex++] = currentFrame;
                        }
                        else
                        {
                            currentFrame.Dispose();
                        }
                        if (i >= endingFrame)
                        {
                            break;
                        }
                    }
                    videoReader.Close();
                    video = new DisplayForm(currentFrames);
                    video.Show();
                }
                else
                {
                    playEndingFrame();
                }
            }
        }

        private void pictureBox4_Click(object sender, EventArgs e)
        {
            if (pictureBox4.Image != null)
            {
                int offSet = page * 20;
                int startingFrame = frameList[3 + offSet] + 1; //acquire starting frame
                if (3 + offSet + 1 < frameList.Count)          //Check if next frame is ending frame
                {
                    int endingFrame = frameList[3 + offSet + 1];
                    Bitmap[] currentFrames = new Bitmap[endingFrame - startingFrame];
                    int framesIndex = 0;
                    VideoFileReader videoReader = new VideoFileReader();
                    videoReader.Open(videoTitle);

                    for (int i = 0; i < 5000; i++)
                    {
                        Bitmap currentFrame = videoReader.ReadVideoFrame();
                        if (i >= startingFrame && i < endingFrame)
                        {
                            currentFrames[framesIndex++] = currentFrame;
                        }
                        else
                        {
                            currentFrame.Dispose();
                        }
                        if (i >= endingFrame)
                        {
                            break;
                        }
                    }
                    videoReader.Close();
                    video = new DisplayForm(currentFrames);
                    video.Show();
                }
                else
                {
                    playEndingFrame();
                }
            }
        }

        private void pictureBox5_Click(object sender, EventArgs e)
        {
            if (pictureBox5.Image != null)
            {
                int offSet = page * 20;
                int startingFrame = frameList[4 + offSet] + 1; //acquire starting frame
                if (4 + offSet + 1 < frameList.Count)          //Check if next frame is ending frame
                {
                    int endingFrame = frameList[4 + offSet + 1];
                    Bitmap[] currentFrames = new Bitmap[endingFrame - startingFrame];
                    int framesIndex = 0;
                    VideoFileReader videoReader = new VideoFileReader();
                    videoReader.Open(videoTitle);

                    for (int i = 0; i < 5000; i++)
                    {
                        Bitmap currentFrame = videoReader.ReadVideoFrame();
                        if (i >= startingFrame && i < endingFrame)
                        {
                            currentFrames[framesIndex++] = currentFrame;
                        }
                        else
                        {
                            currentFrame.Dispose();
                        }
                        if (i >= endingFrame)
                        {
                            break;
                        }
                    }
                    videoReader.Close();
                    video = new DisplayForm(currentFrames);
                    video.Show();
                }
                else
                {
                    playEndingFrame();
                }
            }
        }

        private void pictureBox6_Click(object sender, EventArgs e)
        {
            if (pictureBox6.Image != null)
            {
                int offSet = page * 20;
                int startingFrame = frameList[5 + offSet] + 1; //acquire starting frame
                if (5 + offSet + 1 < frameList.Count)          //Check if next frame is ending frame
                {
                    int endingFrame = frameList[5 + offSet + 1];
                    Bitmap[] currentFrames = new Bitmap[endingFrame - startingFrame];
                    int framesIndex = 0;
                    VideoFileReader videoReader = new VideoFileReader();
                    videoReader.Open(videoTitle);

                    for (int i = 0; i < 5000; i++)
                    {
                        Bitmap currentFrame = videoReader.ReadVideoFrame();
                        if (i >= startingFrame && i < endingFrame)
                        {
                            currentFrames[framesIndex++] = currentFrame;
                        }
                        else
                        {
                            currentFrame.Dispose();
                        }
                        if (i >= endingFrame)
                        {
                            break;
                        }
                    }
                    videoReader.Close();
                    video = new DisplayForm(currentFrames);
                    video.Show();
                }
                else
                {
                    playEndingFrame();
                }
            }
        }

        private void pictureBox7_Click(object sender, EventArgs e)
        {
            if (pictureBox7.Image != null)
            {
                int offSet = page * 20;
                int startingFrame = frameList[6 + offSet] + 1; //acquire starting frame
                if (6 + offSet + 1 < frameList.Count)          //Check if next frame is ending frame
                {
                    int endingFrame = frameList[6 + offSet + 1];
                    Bitmap[] currentFrames = new Bitmap[endingFrame - startingFrame];
                    int framesIndex = 0;
                    VideoFileReader videoReader = new VideoFileReader();
                    videoReader.Open(videoTitle);

                    for (int i = 0; i < 5000; i++)
                    {
                        Bitmap currentFrame = videoReader.ReadVideoFrame();
                        if (i >= startingFrame && i < endingFrame)
                        {
                            currentFrames[framesIndex++] = currentFrame;
                        }
                        else
                        {
                            currentFrame.Dispose();
                        }
                        if (i >= endingFrame)
                        {
                            break;
                        }
                    }
                    videoReader.Close();
                    video = new DisplayForm(currentFrames);
                    video.Show();
                }
                else
                {
                    playEndingFrame();
                }
            }
        }

        private void pictureBox8_Click(object sender, EventArgs e)
        {
            if (pictureBox8.Image != null)
            {
                int offSet = page * 20;
                int startingFrame = frameList[7 + offSet] + 1; //acquire starting frame
                if (7 + offSet + 1 < frameList.Count)          //Check if next frame is ending frame
                {
                    int endingFrame = frameList[7 + offSet + 1];
                    Bitmap[] currentFrames = new Bitmap[endingFrame - startingFrame];
                    int framesIndex = 0;
                    VideoFileReader videoReader = new VideoFileReader();
                    videoReader.Open(videoTitle);

                    for (int i = 0; i < 5000; i++)
                    {
                        Bitmap currentFrame = videoReader.ReadVideoFrame();
                        if (i >= startingFrame && i < endingFrame)
                        {
                            currentFrames[framesIndex++] = currentFrame;
                        }
                        else
                        {
                            currentFrame.Dispose();
                        }
                        if (i >= endingFrame)
                        {
                            break;
                        }
                    }
                    videoReader.Close();
                    video = new DisplayForm(currentFrames);
                    video.Show();
                }
                else
                {
                    playEndingFrame();
                }
            }
        }

        private void pictureBox9_Click(object sender, EventArgs e)
        {
            if (pictureBox9.Image != null)
            {
                int offSet = page * 20;
                int startingFrame = frameList[8 + offSet] + 1; //acquire starting frame
                if (8 + offSet + 1 < frameList.Count)          //Check if next frame is ending frame
                {
                    int endingFrame = frameList[8 + offSet + 1];
                    Bitmap[] currentFrames = new Bitmap[endingFrame - startingFrame];
                    int framesIndex = 0;
                    VideoFileReader videoReader = new VideoFileReader();
                    videoReader.Open(videoTitle);

                    for (int i = 0; i < 5000; i++)
                    {
                        Bitmap currentFrame = videoReader.ReadVideoFrame();
                        if (i >= startingFrame && i < endingFrame)
                        {
                            currentFrames[framesIndex++] = currentFrame;
                        }
                        else
                        {
                            currentFrame.Dispose();
                        }
                        if (i >= endingFrame)
                        {
                            break;
                        }
                    }
                    videoReader.Close();
                    video = new DisplayForm(currentFrames);
                    video.Show();
                }
                else
                {
                    playEndingFrame();
                }
            }
        }

        private void pictureBox10_Click(object sender, EventArgs e)
        {
            if (pictureBox10.Image != null)
            {
                int offSet = page * 20;
                int startingFrame = frameList[9 + offSet] + 1; //acquire starting frame
                if (9 + offSet + 1 < frameList.Count)          //Check if next frame is ending frame
                {
                    int endingFrame = frameList[9 + offSet + 1];
                    Bitmap[] currentFrames = new Bitmap[endingFrame - startingFrame];
                    int framesIndex = 0;
                    VideoFileReader videoReader = new VideoFileReader();
                    videoReader.Open(videoTitle);

                    for (int i = 0; i < 5000; i++)
                    {
                        Bitmap currentFrame = videoReader.ReadVideoFrame();
                        if (i >= startingFrame && i < endingFrame)
                        {
                            currentFrames[framesIndex++] = currentFrame;
                        }
                        else
                        {
                            currentFrame.Dispose();
                        }
                        if (i >= endingFrame)
                        {
                            break;
                        }
                    }
                    videoReader.Close();
                    video = new DisplayForm(currentFrames);
                    video.Show();
                }
                else
                {
                    playEndingFrame();
                }
            }
        }

        private void pictureBox11_Click(object sender, EventArgs e)
        {
            if (pictureBox11.Image != null)
            {
                int offSet = page * 20;
                int startingFrame = frameList[10 + offSet] + 1; //acquire starting frame
                if (10 + offSet + 1 < frameList.Count)          //Check if next frame is ending frame
                {
                    int endingFrame = frameList[10 + offSet + 1];
                    Bitmap[] currentFrames = new Bitmap[endingFrame - startingFrame];
                    int framesIndex = 0;
                    VideoFileReader videoReader = new VideoFileReader();
                    videoReader.Open(videoTitle);

                    for (int i = 0; i < 5000; i++)
                    {
                        Bitmap currentFrame = videoReader.ReadVideoFrame();
                        if (i >= startingFrame && i < endingFrame)
                        {
                            currentFrames[framesIndex++] = currentFrame;
                        }
                        else
                        {
                            currentFrame.Dispose();
                        }
                        if (i >= endingFrame)
                        {
                            break;
                        }
                    }
                    videoReader.Close();
                    video = new DisplayForm(currentFrames);
                    video.Show();
                }
                else
                {
                    playEndingFrame();
                }
            }
        }

        private void pictureBox12_Click(object sender, EventArgs e)
        {
            if (pictureBox12.Image != null)
            {
                int offSet = page * 20;
                int startingFrame = frameList[11 + offSet] + 1; //acquire starting frame
                if (11 + offSet + 1 < frameList.Count)          //Check if next frame is ending frame
                {
                    int endingFrame = frameList[11 + offSet + 1];
                    Bitmap[] currentFrames = new Bitmap[endingFrame - startingFrame];
                    int framesIndex = 0;
                    VideoFileReader videoReader = new VideoFileReader();
                    videoReader.Open(videoTitle);

                    for (int i = 0; i < 5000; i++)
                    {
                        Bitmap currentFrame = videoReader.ReadVideoFrame();
                        if (i >= startingFrame && i < endingFrame)
                        {
                            currentFrames[framesIndex++] = currentFrame;
                        }
                        else
                        {
                            currentFrame.Dispose();
                        }
                        if (i >= endingFrame)
                        {
                            break;
                        }
                    }
                    videoReader.Close();
                    video = new DisplayForm(currentFrames);
                    video.Show();
                }
                else
                {
                    playEndingFrame();
                }
            }
        }

        private void pictureBox13_Click(object sender, EventArgs e)
        {
            if (pictureBox13.Image != null)
            {
                int offSet = page * 20;
                int startingFrame = frameList[12 + offSet] + 1; //acquire starting frame
                if (12 + offSet + 1 < frameList.Count)          //Check if next frame is ending frame
                {
                    int endingFrame = frameList[12 + offSet + 1];
                    Bitmap[] currentFrames = new Bitmap[endingFrame - startingFrame];
                    int framesIndex = 0;
                    VideoFileReader videoReader = new VideoFileReader();
                    videoReader.Open(videoTitle);

                    for (int i = 0; i < 5000; i++)
                    {
                        Bitmap currentFrame = videoReader.ReadVideoFrame();
                        if (i >= startingFrame && i < endingFrame)
                        {
                            currentFrames[framesIndex++] = currentFrame;
                        }
                        else
                        {
                            currentFrame.Dispose();
                        }
                        if (i >= endingFrame)
                        {
                            break;
                        }
                    }
                    videoReader.Close();
                    video = new DisplayForm(currentFrames);
                    video.Show();
                }
                else
                {
                    playEndingFrame();
                }
            }
        }

        private void pictureBox14_Click(object sender, EventArgs e)
        {
            if (pictureBox14.Image != null)
            {
                int offSet = page * 20;
                int startingFrame = frameList[13 + offSet] + 1; //acquire starting frame
                if (13 + offSet + 1 < frameList.Count)          //Check if next frame is ending frame
                {
                    int endingFrame = frameList[13 + offSet + 1];
                    Bitmap[] currentFrames = new Bitmap[endingFrame - startingFrame];
                    int framesIndex = 0;
                    VideoFileReader videoReader = new VideoFileReader();
                    videoReader.Open(videoTitle);

                    for (int i = 0; i < 5000; i++)
                    {
                        Bitmap currentFrame = videoReader.ReadVideoFrame();
                        if (i >= startingFrame && i < endingFrame)
                        {
                            currentFrames[framesIndex++] = currentFrame;
                        }
                        else
                        {
                            currentFrame.Dispose();
                        }
                        if (i >= endingFrame)
                        {
                            break;
                        }
                    }
                    videoReader.Close();
                    video = new DisplayForm(currentFrames);
                    video.Show();
                }
                else
                {
                    playEndingFrame();
                }
            }
        }

        private void pictureBox15_Click(object sender, EventArgs e)
        {
            if (pictureBox15.Image != null)
            {
                int offSet = page * 20;
                int startingFrame = frameList[14 + offSet] + 1; //acquire starting frame
                if (14 + offSet + 1 < frameList.Count)          //Check if next frame is ending frame
                {
                    int endingFrame = frameList[14 + offSet + 1];
                    Bitmap[] currentFrames = new Bitmap[endingFrame - startingFrame];
                    int framesIndex = 0;
                    VideoFileReader videoReader = new VideoFileReader();
                    videoReader.Open(videoTitle);

                    for (int i = 0; i < 5000; i++)
                    {
                        Bitmap currentFrame = videoReader.ReadVideoFrame();
                        if (i >= startingFrame && i < endingFrame)
                        {
                            currentFrames[framesIndex++] = currentFrame;
                        }
                        else
                        {
                            currentFrame.Dispose();
                        }
                        if (i >= endingFrame)
                        {
                            break;
                        }
                    }
                    videoReader.Close();
                    video = new DisplayForm(currentFrames);
                    video.Show();
                }
                else
                {
                    playEndingFrame();
                }
            }
        }

        private void pictureBox16_Click(object sender, EventArgs e)
        {
            if (pictureBox16.Image != null)
            {
                int offSet = page * 20;
                int startingFrame = frameList[15 + offSet] + 1; //acquire starting frame
                if (15 + offSet + 1 < frameList.Count)          //Check if next frame is ending frame
                {
                    int endingFrame = frameList[15 + offSet + 1];
                    Bitmap[] currentFrames = new Bitmap[endingFrame - startingFrame];
                    int framesIndex = 0;
                    VideoFileReader videoReader = new VideoFileReader();
                    videoReader.Open(videoTitle);

                    for (int i = 0; i < 5000; i++)
                    {
                        Bitmap currentFrame = videoReader.ReadVideoFrame();
                        if (i >= startingFrame && i < endingFrame)
                        {
                            currentFrames[framesIndex++] = currentFrame;
                        }
                        else
                        {
                            currentFrame.Dispose();
                        }
                        if (i >= endingFrame)
                        {
                            break;
                        }
                    }
                    videoReader.Close();
                    video = new DisplayForm(currentFrames);
                    video.Show();
                }
                else
                {
                    playEndingFrame();
                }
            }
        }

        private void pictureBox17_Click(object sender, EventArgs e)
        {
            if (pictureBox17.Image != null)
            {
                int offSet = page * 20;
                int startingFrame = frameList[16 + offSet] + 1; //acquire starting frame
                if (16 + offSet + 1 < frameList.Count)          //Check if next frame is ending frame
                {
                    int endingFrame = frameList[16 + offSet + 1];
                    Bitmap[] currentFrames = new Bitmap[endingFrame - startingFrame];
                    int framesIndex = 0;
                    VideoFileReader videoReader = new VideoFileReader();
                    videoReader.Open(videoTitle);

                    for (int i = 0; i < 5000; i++)
                    {
                        Bitmap currentFrame = videoReader.ReadVideoFrame();
                        if (i >= startingFrame && i < endingFrame)
                        {
                            currentFrames[framesIndex++] = currentFrame;
                        }
                        else
                        {
                            currentFrame.Dispose();
                        }
                        if (i >= endingFrame)
                        {
                            break;
                        }
                    }
                    videoReader.Close();
                    video = new DisplayForm(currentFrames);
                    video.Show();
                }
                else
                {
                    playEndingFrame();
                }
            }
        }

        private void pictureBox18_Click(object sender, EventArgs e)
        {
            if (pictureBox18.Image != null)
            {
                int offSet = page * 20;
                int startingFrame = frameList[17 + offSet] + 1; //acquire starting frame
                if (17 + offSet + 1 < frameList.Count)          //Check if next frame is ending frame
                {
                    int endingFrame = frameList[17 + offSet + 1];
                    Bitmap[] currentFrames = new Bitmap[endingFrame - startingFrame];
                    int framesIndex = 0;
                    VideoFileReader videoReader = new VideoFileReader();
                    videoReader.Open(videoTitle);

                    for (int i = 0; i < 5000; i++)
                    {
                        Bitmap currentFrame = videoReader.ReadVideoFrame();
                        if (i >= startingFrame && i < endingFrame)
                        {
                            currentFrames[framesIndex++] = currentFrame;
                        }
                        else
                        {
                            currentFrame.Dispose();
                        }
                        if (i >= endingFrame)
                        {
                            break;
                        }
                    }
                    videoReader.Close();
                    video = new DisplayForm(currentFrames);
                    video.Show();
                }
                else
                {
                    playEndingFrame();
                }
            }
        }

        private void pictureBox19_Click(object sender, EventArgs e)
        {
            if (pictureBox19.Image != null)
            {
                int offSet = page * 20;
                int startingFrame = frameList[18 + offSet] + 1; //acquire starting frame
                if (18 + offSet + 1 < frameList.Count)          //Check if next frame is ending frame
                {
                    int endingFrame = frameList[18 + offSet + 1];
                    Bitmap[] currentFrames = new Bitmap[endingFrame - startingFrame];
                    int framesIndex = 0;
                    VideoFileReader videoReader = new VideoFileReader();
                    videoReader.Open(videoTitle);

                    for (int i = 0; i < 5000; i++)
                    {
                        Bitmap currentFrame = videoReader.ReadVideoFrame();
                        if (i >= startingFrame && i < endingFrame)
                        {
                            currentFrames[framesIndex++] = currentFrame;
                        }
                        else
                        {
                            currentFrame.Dispose();
                        }
                        if (i >= endingFrame)
                        {
                            break;
                        }
                    }
                    videoReader.Close();
                    video = new DisplayForm(currentFrames);
                    video.Show();
                }
                else
                {
                    playEndingFrame();
                }
            }
        }

        private void pictureBox20_Click(object sender, EventArgs e)
        {
            if (pictureBox20.Image != null)
            {
                int offSet = page * 20;
                int startingFrame = frameList[19 + offSet] + 1; //acquire starting frame
                if (19 + offSet + 1 < frameList.Count)          //Check if next frame is ending frame
                {
                    int endingFrame = frameList[19 + offSet + 1];
                    Bitmap[] currentFrames = new Bitmap[endingFrame - startingFrame];
                    int framesIndex = 0;
                    VideoFileReader videoReader = new VideoFileReader();
                    videoReader.Open(videoTitle);

                    for (int i = 0; i < 5000; i++)
                    {
                        Bitmap currentFrame = videoReader.ReadVideoFrame();
                        if (i >= startingFrame && i < endingFrame)
                        {
                            currentFrames[framesIndex++] = currentFrame;
                        }
                        else
                        {
                            currentFrame.Dispose();
                        }
                        if (i >= endingFrame)
                        {
                            break;
                        }
                    }
                    videoReader.Close();
                    video = new DisplayForm(currentFrames);
                    video.Show();
                }
                else
                {
                    playEndingFrame();
                }
            }
        }
        //Special case for the ending frame
        private void playEndingFrame()
        {
            int lastFrame = 0;
            for(int i = 0; i < transitions.Length; i++)
            {
                if(transitions[i].start == int.MaxValue)
                {
                    lastFrame = i - 1;  //Grab last frame index in transitions
                    break;
                }
            }
            int difference = transitions[lastFrame].end + 1 - transitions[lastFrame].start + 1; //Acquire difference between starting/ending frame
            int start = transitions[lastFrame].start + 1;
            int end = transitions[lastFrame].end;
            int framesIndex = 0;
            Bitmap[] currentFrames = new Bitmap[difference];  //Make array with frame difference size
            VideoFileReader videoReader = new VideoFileReader();
            videoReader.Open(videoTitle);
            for (int i = 0; i < 5000; i++)
            {
                Bitmap currentFrame = videoReader.ReadVideoFrame();
                if (i >= start && i < end)
                {
                    currentFrames[framesIndex++] = currentFrame; //Add bitmap to array if within frame range
                }
                else
                {
                    currentFrame.Dispose();  //Dispose of bitmap  if not within range
                }
                if (i >= end)
                {
                    break;
                }
            }
            videoReader.Close();
            video = new DisplayForm(currentFrames); //Send in bitmap array to display video
            video.Show();      //Show video
        }
        
        //method needed to create thumbnails
        public bool ThumbnailCallback()
        {
            return true;
        }

    }
}


