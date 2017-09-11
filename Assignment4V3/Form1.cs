//---------------------------------------------------------------------------
// Form1.cs
// Geoffrey Lebbos CSS 490 B
// 6/02/2017
// Last Modified: 6/4/2017
//---------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using AForge.Video.FFMPEG;
using System.IO;

namespace Assignment4V3
{
    public partial class Form1 : Form
    {
        string videoFileName;
        ResultofSearch resultForm;
        public Form1()
        {
            InitializeComponent();
        }
        private void button1_Click_1(object sender, EventArgs e)
        {
            //Read only avi video files
            openFileDialog1.Filter = "AVI Files|*.avi| MPG Files|*.mpg";
            openFileDialog1.FilterIndex = 1;
            openFileDialog1.FileName = "";
            openFileDialog1.ShowDialog();
            if (openFileDialog1.FileName != "")
            {
                //open the file from the dialog and show it in the picture box
                videoFileName = openFileDialog1.FileName;
                VideoFileReader videoReader = new VideoFileReader();
                videoReader.Open(videoFileName);

                //Show starting frame
                Bitmap startingFrame = videoReader.ReadVideoFrame();
                int hor = startingFrame.Height;
                int width = startingFrame.Width;
                Size newS = new Size(width, hor);
                queryPicture.Size = newS;
                Point newP = new Point(queryPicture.Location.X + width + 15, button1.Location.Y);
                queryPicture.Image = startingFrame;
                videoReader.Close();
            }
        }

        private void button2_Click_1(object sender, EventArgs e)
        {
            if (queryPicture.Image != null)
            {
                //Grab info for file
                FileInfo newFile = new FileInfo(videoFileName);
                string path = newFile.DirectoryName;
                resultForm = new ResultofSearch(this);
                //process video
                resultForm.processVideo(videoFileName, path);
                //show results
                resultForm.Show();
                this.Hide();
            }
        }
    }
}
