//---------------------------------------------------------------------------
// Program.cs
// Geoffrey Lebbos CSS 490 B
// 6/02/2017
// Last Modified: 6/4/2017
//---------------------------------------------------------------------------
//------------------------IMPLEMENTATION DETAILS-----------------------------
//Video files loaded into this program are expected to be .avi
//Video frames will be displayed 20 per page, with a total of pages dependsing on size
//Clicking on a video frame will display a video of the scene up until the next picturebox frame
/*The very first video process done with this program will take 7 minutes to process if differences.txt
does not exist in the debug folder.Every process after that should be quick UNLESS the text file
is deleted*/
//---------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Assignment4V3
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());
        }
    }
}
