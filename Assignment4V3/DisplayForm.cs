//---------------------------------------------------------------------------
// DisplayForm.cs
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

namespace Assignment4V3
{
    //Form responsible for playing video on picturebox click
    public partial class DisplayForm : Form
    {
        //Bitmap array to copy bitmap parameter passed in
        Bitmap[] temp;
        //Index for traversing temp
        int index = 0;
        public DisplayForm(Bitmap [] array)
        {
            InitializeComponent();
            temp = array;    //Copy array into temp
            timer1 = new Timer();   //Initialize timer object
            timer1.Interval = 1000 / 25;  //Set interval for displaying frames
            timer1.Enabled = true;  //Enable t imer
            timer1.Tick += timer1tick;  //Tick until timer is disabled            
        }
        //Display frames as a video based on tick timer
        private void timer1tick(object sender, EventArgs e)
        {
            //Loop until array length
            if(index < temp.Length)
            {
                //If array index contains a bitmap
                if (temp[index] != null)
                {
                    //display bitmap in picturebox frame
                    Image newImag1 = temp[index++].GetThumbnailImage(300, 300, new System.Drawing.Image.GetThumbnailImageAbort(ThumbnailCallback), IntPtr.Zero);
                    pictureBox1.Image = newImag1;
                }
            }
            else
            {
                //Reached end of the array, disable timer
                timer1.Enabled = false;
            }
        }
        //Used for gaining thumbnail
        public bool ThumbnailCallback()
        {
            return true;
        }
    }
}
