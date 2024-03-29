using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;

using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using System.Xml;

/* TODO:
 * 
 * Fix NI-stem to be able to use different bitrates (higher than 128k)
 * 
 * 
 * Make the panels a groupbox and disable all of them when running, so the user can't screw things up (right now the user can uncheck things)
 * /\ - Changing checkbox states while the program is running crashes things, but the user has to be pretty stupid to try this
 * Refactor the Stem-building and ffmpeg stuff
 * 
 * 
 * */

namespace SpleeterGui
{
    public partial class Form1 : Form
    {
        private bool stemSyncerBackup = false;
        private string stem_count = "2";
        private string mask_extension = "average";
        private string storage = "";

        private string path_python = "";    //needs to be the SpleeterGUI folder, not python

        private string current_song = "";
        private string current_songname = "";
        private int files_remain = 0;
        private List<string> files_to_process = new List<string>();
        private Boolean run_silent = true;
        private String gui_version = "";
        IDictionary<string, string> langStr = new Dictionary<string, string>();

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            this.AllowDrop = true;
            this.DragEnter += new DragEventHandler(Form1_DragEnter);
            this.DragDrop += new DragEventHandler(Form1_DragDrop);
        }

        private void Form1_Shown(object sender, EventArgs e)
        {
            LoadStuff();
        }

        public void LoadStuff()
        {
            //program startup - initialise things
            txt_output_directory.Text = Properties.Settings.Default.output_location;
            cmbBox_codec.SelectedIndex = Properties.Settings.Default.codec;
            chkSongName.Checked = Properties.Settings.Default.songName;
            txt_collection_path.Text = Properties.Settings.Default.collection_location;
            txt_collection_path_out.Text = Properties.Settings.Default.collection_out_location;

            if (Properties.Settings.Default.path_python == "")
            {
                path_python = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\SpleeterGUI\python";
                storage = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\SpleeterGUI";
            }
            else
            {
                path_python = Properties.Settings.Default.path_python + @"\python";
                storage = Properties.Settings.Default.path_python;
            }


            gui_version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            String version = Assembly.GetExecutingAssembly().GetName().Version.Major.ToString() + "." + Assembly.GetExecutingAssembly().GetName().Version.Minor.ToString();
            this.Text = "SpleeterGUI " + version;



            bitrate.Value = Properties.Settings.Default.bitrate;
            duration.Value = Properties.Settings.Default.duration;

            update_checks();
            get_languages();
            update_language(Properties.Settings.Default.language);

            string txt = langStr["LoadStuff_textBox1"];
            txt = txt.Replace("[NL]", "\r\n");
            textBox1.Text = txt + "...\r\n";
            run_cmd("pip show spleeter");

            textBox1.AppendText(storage + "\r\n");
        }

        void get_languages()
        {
            //find and load language files in to menu toolstrip
            ToolStrip language_menu = new ToolStrip();
            var enviroment = System.Environment.CurrentDirectory;
            string[] fileEntries = Directory.GetFiles(enviroment + "\\languages");


            ToolStripMenuItem[] items = new ToolStripMenuItem[fileEntries.Length];
            int i = 0;
            foreach (string fileName in fileEntries)
            {
                string name = Path.GetFileName(fileName);
                XmlDataDocument xmldoc = new XmlDataDocument();
                XmlNodeList xmlnode;
                FileStream fs = new FileStream(enviroment + "\\languages\\" + name, FileMode.Open, FileAccess.Read);
                xmldoc.Load(fs);
                xmlnode = xmldoc.GetElementsByTagName("language");
                string lang_text = xmlnode[0].ChildNodes.Item(0).InnerText.Trim();

                items[i] = new ToolStripMenuItem();
                items[i].Text = lang_text + " (" + name.Replace(".xml", "") + ")";
                items[i].Tag = name.Replace(".xml", "");
                items[i].Click += new EventHandler(LanguageItemClickHandler);
                i++;
            }
            this.mnuLanguage.DropDownItems.AddRange(items);
        }

        private void LanguageItemClickHandler(object sender, EventArgs e)
        {
            //a language is chosen by the user, load it up
            ToolStripMenuItem clickedItem = (ToolStripMenuItem)sender;
            update_language(clickedItem.Tag.ToString());
        }

        void update_language(string lang_name)
        {
            // Read the XML language files, iterate through menu's & controls and update labels.
            Properties.Settings.Default.language = lang_name;
            Properties.Settings.Default.Save();
            XmlDataDocument xmldoc = new XmlDataDocument();
            XmlNodeList xmlnode;
            int i = 0;
            string control_name = null;
            string control_label = null;
            var enviroment = System.Environment.CurrentDirectory;
            FileStream fs = new FileStream(enviroment + "\\languages\\" + lang_name + ".xml", FileMode.Open, FileAccess.Read);
            xmldoc.Load(fs);
            xmlnode = xmldoc.GetElementsByTagName("item");  //load control texts
            for (i = 0; i <= xmlnode.Count - 1; i++)
            {
                xmlnode[i].ChildNodes.Item(0).InnerText.Trim();
                control_label = xmlnode[i].ChildNodes.Item(0).InnerText.Trim();
                control_name = xmlnode[i].Attributes["control"].InnerText;

                Control ctn = Controls.Find(control_name, true)[0];
                ctn.Text = control_label;
            }
            xmlnode = xmldoc.GetElementsByTagName("menu");  //load menu texts
            for (i = 0; i <= xmlnode.Count - 1; i++)
            {
                xmlnode[i].ChildNodes.Item(0).InnerText.Trim();
                control_label = xmlnode[i].ChildNodes.Item(0).InnerText.Trim();
                control_name = xmlnode[i].Attributes["control"].InnerText;

                foreach (ToolStripMenuItem item in menuStrip1.Items)
                {
                    if (item.Name == control_name)
                    {
                        item.Text = control_label;
                    }
                    foreach (ToolStripMenuItem subitem in item.DropDownItems.OfType<ToolStripMenuItem>())
                    {
                        if (subitem.Name == control_name)
                        {
                            subitem.Text = control_label;
                        }
                    }
                }
            }
            xmlnode = xmldoc.GetElementsByTagName("lang");  //load all the program texts
            for (i = 0; i <= xmlnode.Count - 1; i++)
            {
                xmlnode[i].ChildNodes.Item(0).InnerText.Trim();
                control_label = xmlnode[i].ChildNodes.Item(0).InnerText.Trim();
                control_name = xmlnode[i].Attributes["control"].InnerText;
                langStr[control_name] = control_label;
            }
            progress_txt.Text = langStr["idle"];
        }

        void Form1_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy;
        }

        void Form1_DragDrop(object sender, DragEventArgs e)
        {
            //music files have been dropped on the app, start processing them
            if (files_remain == 0)
            {
                textBox1.Text = "";
                if (txt_output_directory.Text == "")
                {
                    MessageBox.Show(langStr["output_message"]);
                    return;
                }

                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                files_remain = 0;
                foreach (string file in files)
                {
                    files_to_process.Add(file);
                    files_remain++;
                }
                textBox1.AppendText(langStr["starting_all"] + "\r\n");
                progressBar1.Maximum = files_remain + 1;
                progressBar1.Value = 0;
                progress_txt.Text = langStr["starting"] + "..." + files_remain + " " + langStr["songs_remaining"];
                next_song();
            }
            else
            {
                System.Media.SystemSounds.Asterisk.Play();
            }
        }

        private void checkForSpleeterError()
        {
            bool somethingWentWrong;

            if (chkSongName.Checked == true)
            {


                somethingWentWrong = (current_songname != "" &&
                ((stem_count == "2" &&
                (!File.Exists(txt_output_directory.Text + @"\" + current_songname + @"\" + current_songname + " - vocals." + cmbBox_codec.GetItemText(cmbBox_codec.SelectedItem)) ||
                !File.Exists(txt_output_directory.Text + @"\" + current_songname + @"\" + current_songname + " - accompaniment." + cmbBox_codec.GetItemText(cmbBox_codec.SelectedItem)))
                ) ||
                (stem_count == "4" &&
                (!File.Exists(txt_output_directory.Text + @"\" + current_songname + @"\" + current_songname + " - vocals." + cmbBox_codec.GetItemText(cmbBox_codec.SelectedItem)) ||
                !File.Exists(txt_output_directory.Text + @"\" + current_songname + @"\" + current_songname + " - bass." + cmbBox_codec.GetItemText(cmbBox_codec.SelectedItem)) ||
                !File.Exists(txt_output_directory.Text + @"\" + current_songname + @"\" + current_songname + " - drums." + cmbBox_codec.GetItemText(cmbBox_codec.SelectedItem)) ||
                !File.Exists(txt_output_directory.Text + @"\" + current_songname + @"\" + current_songname + " - other." + cmbBox_codec.GetItemText(cmbBox_codec.SelectedItem)))
                ) ||
                (stem_count == "5" &&
                (!File.Exists(txt_output_directory.Text + @"\" + current_songname + @"\" + current_songname + " - vocals." + cmbBox_codec.GetItemText(cmbBox_codec.SelectedItem)) ||
                !File.Exists(txt_output_directory.Text + @"\" + current_songname + @"\" + current_songname + " - bass." + cmbBox_codec.GetItemText(cmbBox_codec.SelectedItem)) ||
                !File.Exists(txt_output_directory.Text + @"\" + current_songname + @"\" + current_songname + " - drums." + cmbBox_codec.GetItemText(cmbBox_codec.SelectedItem)) ||
                !File.Exists(txt_output_directory.Text + @"\" + current_songname + @"\" + current_songname + " - piano." + cmbBox_codec.GetItemText(cmbBox_codec.SelectedItem)) ||
                !File.Exists(txt_output_directory.Text + @"\" + current_songname + @"\" + current_songname + " - other." + cmbBox_codec.GetItemText(cmbBox_codec.SelectedItem))))));
            }
            else
            {
                somethingWentWrong = (current_songname != "" &&
                ((stem_count == "2" &&
                (!File.Exists(txt_output_directory.Text + @"\" + current_songname + @"\" + "vocals." + cmbBox_codec.GetItemText(cmbBox_codec.SelectedItem)) ||
                !File.Exists(txt_output_directory.Text + @"\" + current_songname + @"\" + "accompaniment." + cmbBox_codec.GetItemText(cmbBox_codec.SelectedItem)))
                ) ||
                (stem_count == "4" &&
                (!File.Exists(txt_output_directory.Text + @"\" + current_songname + @"\" + "vocals." + cmbBox_codec.GetItemText(cmbBox_codec.SelectedItem)) ||
                !File.Exists(txt_output_directory.Text + @"\" + current_songname + @"\" + "bass." + cmbBox_codec.GetItemText(cmbBox_codec.SelectedItem)) ||
                !File.Exists(txt_output_directory.Text + @"\" + current_songname + @"\" + "drums." + cmbBox_codec.GetItemText(cmbBox_codec.SelectedItem)) ||
                !File.Exists(txt_output_directory.Text + @"\" + current_songname + @"\" + "other." + cmbBox_codec.GetItemText(cmbBox_codec.SelectedItem)))
                ) ||
                (stem_count == "5" &&
                (!File.Exists(txt_output_directory.Text + @"\" + current_songname + @"\" + "vocals." + cmbBox_codec.GetItemText(cmbBox_codec.SelectedItem)) ||
                !File.Exists(txt_output_directory.Text + @"\" + current_songname + @"\" + "bass." + cmbBox_codec.GetItemText(cmbBox_codec.SelectedItem)) ||
                !File.Exists(txt_output_directory.Text + @"\" + current_songname + @"\" + "drums." + cmbBox_codec.GetItemText(cmbBox_codec.SelectedItem)) ||
                !File.Exists(txt_output_directory.Text + @"\" + current_songname + @"\" + "piano." + cmbBox_codec.GetItemText(cmbBox_codec.SelectedItem)) ||
                !File.Exists(txt_output_directory.Text + @"\" + current_songname + @"\" + "other." + cmbBox_codec.GetItemText(cmbBox_codec.SelectedItem))))));
            }

            if (somethingWentWrong)
            {
                spleeterError();
            }

        }

        private void spleeterError()
        {
            // Process exited but no files were created!!! Error!
            textBox1.Text += "\r\n \r\n" +
            "Error: Spleeter exited but no files were created! \r\n" +
            "==================================== \r\n" +
            "Files containing spaces at the end e.g. 'songfilename .mp3' are problematic and crash Spleeter. \r\n" +
            "Try renaming the file! \r\n";

            System.Media.SystemSounds.Exclamation.Play();
            MessageBox.Show("Error: Spleeter exited but no files were created! \n" +
                "Files containing spaces at the end e.g. 'songfilename .mp3' are problematic and crash Spleeter. \n" +
                "Try renaming the file!");


            //System.Media.SystemSounds.Exclamation.Play();
            //MessageBox.Show("Error: Spleeter exited but no files were created! \n" +
            //    "The input file somehow crashed Spleeter. This could be due to the file name. \n" +
            //    "Files containing spaces such as 'songfilename .mp3' seem to be problematic. \n" +
            //    "Try renaming the file and please create an issue on GitHub! \n" +
            //    "A log file has been generated in the output folder.");

            //generateLog();

            // Clear the song queue
            files_remain = 0;
            next_song();
        }

        private void generateLog()
        {

            string variables =
            " \n txt_output_directory.Text: \t " + txt_output_directory.Text +
            " \n current_songname: \t " + current_songname +
            " \n cmbBox_codec.GetItemText(cmbBox_codec.SelectedItem): \t " + cmbBox_codec.GetItemText(cmbBox_codec.SelectedItem) + "" +
            " \n storage: \t " + storage +
            " \n path_python: \t " + path_python +
            " \n Environment.CurrentDirectory: \t " + Environment.CurrentDirectory +
            " \n (duration.Value).ToString(): \t " + (duration.Value).ToString() +
            " \n chkSongName.Checked: \t " + chkSongName.Checked +
            " \n stem_count: \t " + stem_count + "" +
            " \n files_remain: \t " + files_remain +
            " \n chkRecombine.Checked: \t " + chkRecombine.Checked +
            " \n chkFullBandwidth.Checked: \t " + chkFullBandwidth.Checked +
            " \n gui_version: \t " + gui_version +
            " \n current_song: \t " + current_song;


            using (StreamWriter outputFile = new StreamWriter(Path.Combine(txt_output_directory.Text, "_SpleeterGUI_log_" + DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss") + ".txt")))
            {
                outputFile.WriteLine(textBox1.Text + "\n \n \n StackTrace: \n \n \n " + Environment.StackTrace + "\n \n \n variables: \n \n \n " + variables);
            }

            textBox1.Text += "\r\n A log file has been created at " + txt_output_directory.Text + "\r\n";
        }

        private void next_song()
        {
            //begins the spleeting function on the next song in the queue
            if (files_remain > 0)
            {
                run_silent = false;
                //string pyPath = storage + @"\python\python.exe";
                string pyPath = path_python + @"\python.exe";

                string filename = files_to_process[0];

                string fullBandWidth = "";
                if (chkFullBandwidth.Checked)
                {
                    fullBandWidth = "-16kHz";
                }

                progressBar1.Value = progressBar1.Value + 1;
                textBox1.AppendText(langStr["processing"] + " " + filename + "\r\n");
                progress_txt.Text = langStr["working"] + "... " + files_remain + " " + langStr["songs_remaining"];
                ProcessStartInfo processStartInfo;
                textBox1.AppendText(("-m spleeter separate -o " + (char)34 + txt_output_directory.Text + (char)34 + " -d " +
                        (duration.Value).ToString() + " -b " + (bitrate.Value).ToString() + "k -p " + (char)34 + "spleeter:" + stem_count + "stems" + fullBandWidth + (char)34 + " -c " + cmbBox_codec.GetItemText(cmbBox_codec.SelectedItem) +
                        " -f " + (char)34 + "{filename}\\{filename} - {instrument}.{codec}" + (char)34 + " " + (char)34 + filename + (char)34) + "\r\n");
                if (chkNIStem.Checked == true)
                {
                    processStartInfo = new ProcessStartInfo(pyPath, @" -m spleeter separate  -o " + (char)34 + txt_output_directory.Text + (char)34 + " -d " +
                        (duration.Value).ToString() + " -b " + (bitrate.Value).ToString() + "k -p " + (char)34 + "spleeter:" + stem_count + "stems" + fullBandWidth + (char)34 + " -c " + cmbBox_codec.GetItemText(cmbBox_codec.SelectedItem) +
                        " -f " + (char)34 + "{filename}\\{filename} - {instrument}.{codec}" + (char)34 + " " + (char)34 + filename + (char)34);
                }
                else if (chkSongName.Checked == true)
                {
                    processStartInfo = new ProcessStartInfo(pyPath, @" -m spleeter separate  -o " + (char)34 + txt_output_directory.Text + (char)34 + " -d " +
                        (duration.Value).ToString() + " -b " + (bitrate.Value).ToString() + "k -p " + (char)34 + "spleeter:" + stem_count + "stems" + fullBandWidth + (char)34 + " -c " + cmbBox_codec.GetItemText(cmbBox_codec.SelectedItem) +
                        " -f " + (char)34 + "{filename}\\{filename} - {instrument}.{codec}" + (char)34 + " " + (char)34 + filename + (char)34);
                }
                else
                {
                    processStartInfo = new ProcessStartInfo(pyPath, @" -m spleeter separate  -o " + (char)34 + txt_output_directory.Text + (char)34 + " -d " +
                        (duration.Value).ToString() + " -b " + (bitrate.Value).ToString() + "k -p " + (char)34 + "spleeter:" + stem_count + "stems" + fullBandWidth + (char)34 + " -c " + cmbBox_codec.GetItemText(cmbBox_codec.SelectedItem) +
                        " " + (char)34 + filename + (char)34);
                }
                processStartInfo.WorkingDirectory = storage;

                processStartInfo.UseShellExecute = false;
                processStartInfo.ErrorDialog = false;
                processStartInfo.RedirectStandardOutput = true;
                processStartInfo.RedirectStandardError = true;
                processStartInfo.CreateNoWindow = true;

                files_to_process.Remove(filename);

                Process process = new Process();
                process.StartInfo = processStartInfo;
                process.EnableRaisingEvents = true;
                process.Exited += new EventHandler(ProcessExited);
                process.OutputDataReceived += new DataReceivedEventHandler(OutputHandler);
                process.ErrorDataReceived += new DataReceivedEventHandler(ErrorHandler);
                try
                {
                    bool processStarted = process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    current_songname = Path.GetFileNameWithoutExtension(filename);
                    current_song = filename;
                }
                catch
                {
                    //"Error: unable to find python.exe"
                    MessageBox.Show(langStr["python_not_found"] + "\n" + langStr["python_path_error_tip"]);
                }
            }
            else
            {
                current_songname = "";
                progress_txt.Text = langStr["idle"];
                textBox1.AppendText(langStr["finished"] + "\r\n");
                progressBar1.Value = progressBar1.Maximum;
                System.Media.SystemSounds.Beep.Play();
            }
        }

        private void run_cmd(String cmd)
        {
            //general function for executing python commands.
            try
            {
                ProcessStartInfo processStartInfo;
                string pyPath = path_python + @"\python.exe";

                processStartInfo = new ProcessStartInfo(pyPath, @" -W ignore -m " + cmd);
                processStartInfo.WorkingDirectory = storage;

                processStartInfo.UseShellExecute = false;
                processStartInfo.ErrorDialog = false;
                processStartInfo.RedirectStandardOutput = true;
                processStartInfo.RedirectStandardError = true;
                processStartInfo.CreateNoWindow = true;
                Process process = new Process();
                process.StartInfo = processStartInfo;
                process.EnableRaisingEvents = true;
                process.Exited += new EventHandler(ProcessExited);
                process.OutputDataReceived += new DataReceivedEventHandler(OutputHandler);
                process.ErrorDataReceived += new DataReceivedEventHandler(ErrorHandler);

                bool processStarted = process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
            }
            catch
            {
                MessageBox.Show(langStr["python_not_found"] + "\n" + langStr["python_path_error_tip"]);
            }
        }
        private void run_recombine(String args)
        {
            //executes the ffmpeg comand to recombine the output stems
            ProcessStartInfo processStartInfo = new ProcessStartInfo(storage + @"\ffmpeg.exe", args);
            processStartInfo.WorkingDirectory = storage;

            processStartInfo.UseShellExecute = false;
            processStartInfo.ErrorDialog = false;
            processStartInfo.RedirectStandardOutput = true;
            processStartInfo.RedirectStandardError = true;
            processStartInfo.CreateNoWindow = true;
            Process process = new Process();
            process.StartInfo = processStartInfo;
            process.EnableRaisingEvents = true;
            process.Exited += new EventHandler(run_recombineExited);
            process.OutputDataReceived += new DataReceivedEventHandler(OutputHandler);
            process.ErrorDataReceived += new DataReceivedEventHandler(ErrorHandler);
            bool processStarted = process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
        }
        void OutputHandler(object sender, DataReceivedEventArgs e)
        {
            //output handler called by run_cmd
            this.BeginInvoke(new MethodInvoker(() =>
            {
                if (!String.IsNullOrEmpty(e.Data))
                {
                    if (txt_check(e.Data))   //Please don't email Deezer about problems with this GUI app.
                    {
                        textBox1.AppendText(e.Data.TrimEnd('\r', '\n') + "\r\n");
                    }
                }
            }));
        }
        bool txt_check(string txt)  //prevent output
        {
            bool allow = true;
            if (txt.IndexOf("Author-email") != -1) { allow = false; }
            if (txt.IndexOf("Summary:") != -1) { allow = false; }
            if (txt.IndexOf("source separation library") != -1) { allow = false; }
            if (txt.IndexOf("models based on") != -1) { allow = false; }
            if (txt.IndexOf("Home-page:") != -1) { allow = false; }
            if (txt.IndexOf("Author:") != -1) { allow = false; }
            if (txt.IndexOf("License:") != -1) { allow = false; }
            if (txt.IndexOf("Location:") != -1) { allow = false; }
            if (txt.IndexOf("Requires:") != -1) { allow = false; }
            if (txt.IndexOf("Required-by:") != -1) { allow = false; }
            return allow;
        }
        void ErrorHandler(object sender, DataReceivedEventArgs e)
        {
            //handles errors from the run_cmd functions
            this.BeginInvoke(new MethodInvoker(() =>
            {
                if (!String.IsNullOrEmpty(e.Data))
                {
                    textBox1.AppendText(e.Data.TrimEnd('\r', '\n') + "\r\n");
                }
            }));
        }
        private void run_recombineExited(object sender, EventArgs e)
        {
            //cleanup function called by run_recombine
            Invoke((Action)(() =>
            {
                //do nothing
            }));
        }

        private void run_ffmpegExited(object sender, EventArgs e)
        {
            //cleanup function called by run_ffmpeg
            Invoke((Action)(() =>
            {
                if (chkNIStemTwoStems.Checked)
                {
                    run_ffmpegTwoStemMakerRunner(current_songname);
                    //This structure is really spaghetti, should be refactored
                }
                run_NIStem();
            }));
        }

        private void run_doNothingOnExit(object sender, EventArgs e)
        {
            //cleanup function
            Invoke((Action)(() =>
            {
                //do nothing
            }));
        }


        private void run_StemSyncerExited(object sender, EventArgs e)
        {
            textBox1.AppendText("\r\n" + "StemSyncer Exited" + "\r\n");
        }

        private void run_niStemExited(object sender, EventArgs e)
        {
            //cleanup function called by run_niStem
            Invoke((Action)(() =>
            {
                if (File.Exists(txt_output_directory.Text + @"\" + current_songname + @"\" + current_songname + " - mix.wav"))
                {
                    File.Delete(txt_output_directory.Text + @"\" + current_songname + @"\" + current_songname + " - mix.wav");
                    // System.Media.SystemSounds.Beep.Play();
                }

                if (chkStemRemoveFiles.Checked == true)
                {
                    RemoveStemFiles();
                }

                if (chkUpdateCollection.Checked == true)
                {
                    textBox1.AppendText("\r\n" + "Starting StemSyncer" + "\r\n");
                    run_StemSyncer();
                }

                files_remain--;
                if (files_remain > -1)
                {
                    //start processing the next song
                    next_song();
                }
                if (files_remain < 0) files_remain = 0;

                if (!run_silent)
                {
                    textBox1.AppendText("\r\n" + langStr["run_complete"] + "\r\n");
                    System.Media.SystemSounds.Beep.Play();
                }

            }));
        }

        private void RemoveStemFiles()
        {
            textBox1.AppendText("\r\n" + "Removing working files" + "\r\n");

            if (File.Exists(txt_output_directory.Text + @"\" + current_songname + @"\" + current_songname + " - other." + cmbBox_codec.GetItemText(cmbBox_codec.SelectedItem)))
            {
                File.Delete(txt_output_directory.Text + @"\" + current_songname + @"\" + current_songname + " - other." + cmbBox_codec.GetItemText(cmbBox_codec.SelectedItem));
            }
            if (File.Exists(txt_output_directory.Text + @"\" + current_songname + @"\" + current_songname + " - mix." + cmbBox_codec.GetItemText(cmbBox_codec.SelectedItem)))
            {
                File.Delete(txt_output_directory.Text + @"\" + current_songname + @"\" + current_songname + " - mix." + cmbBox_codec.GetItemText(cmbBox_codec.SelectedItem));
            }
            if (File.Exists(txt_output_directory.Text + @"\" + current_songname + @"\" + current_songname + " - vocals." + cmbBox_codec.GetItemText(cmbBox_codec.SelectedItem)))
            {
                File.Delete(txt_output_directory.Text + @"\" + current_songname + @"\" + current_songname + " - vocals." + cmbBox_codec.GetItemText(cmbBox_codec.SelectedItem));
            }
            if (File.Exists(txt_output_directory.Text + @"\" + current_songname + @"\" + current_songname + " - bass." + cmbBox_codec.GetItemText(cmbBox_codec.SelectedItem)))
            {
                File.Delete(txt_output_directory.Text + @"\" + current_songname + @"\" + current_songname + " - bass." + cmbBox_codec.GetItemText(cmbBox_codec.SelectedItem));
            }
            if (File.Exists(txt_output_directory.Text + @"\" + current_songname + @"\" + current_songname + " - drums." + cmbBox_codec.GetItemText(cmbBox_codec.SelectedItem)))
            {
                File.Delete(txt_output_directory.Text + @"\" + current_songname + @"\" + current_songname + " - drums." + cmbBox_codec.GetItemText(cmbBox_codec.SelectedItem));
            }
            if (!Directory.EnumerateFileSystemEntries(txt_output_directory.Text + @"\" + current_songname).Any())
            {
                Directory.Delete(txt_output_directory.Text + @"\" + current_songname);
            }
            else
            {
                textBox1.AppendText("\r\n" + "Folder: \"" + txt_output_directory.Text + @"\" + current_songname + "\" is not empty! Not removing folder!" + "\r\n");
            }

        }

        private void recombineAudio()
        {
            String recomnbine_command = "";
            String codec = cmbBox_codec.GetItemText(cmbBox_codec.SelectedItem);
            int input_count = 0;
            if (chkSongName.Checked == false)
            {
                if (chkRPartVocal.Checked) { input_count++; recomnbine_command += " -i " + (char)34 + txt_output_directory.Text + @"\" + current_songname + @"\vocals." + codec + (char)34; }
                if (chkRPartBass.Checked) { input_count++; recomnbine_command += " -i " + (char)34 + txt_output_directory.Text + @"\" + current_songname + @"\bass." + codec + (char)34; }
                if (chkRPartDrums.Checked) { input_count++; recomnbine_command += " -i " + (char)34 + txt_output_directory.Text + @"\" + current_songname + @"\drums." + codec + (char)34; }
                if (chkRPartPiano.Checked) { input_count++; recomnbine_command += " -i " + (char)34 + txt_output_directory.Text + @"\" + current_songname + @"\piano." + codec + (char)34; }
                if (chkRPartOther.Checked) { input_count++; recomnbine_command += " -i " + (char)34 + txt_output_directory.Text + @"\" + current_songname + @"\other." + codec + (char)34; }
                if (recomnbine_command != "")
                {
                    String filter_a = "";
                    String filter_b = "";
                    for (int i = 0; i < input_count; i++)
                    {
                        filter_a += "[" + i + "]volume=" + input_count + "[" + ((char)97 + i) + "];";
                        filter_b += "[" + ((char)97 + i) + "]";
                    }
                    recomnbine_command = recomnbine_command + " -filter_complex " + (char)34 + filter_a + filter_b + "amix=inputs=" + input_count.ToString() +
                        ":duration =longest" + (char)34 + " -ab " + (bitrate.Value).ToString() + "k " + (char)34 + txt_output_directory.Text + @"\" + current_songname + "_recombined."
                        + cmbBox_codec.GetItemText(cmbBox_codec.SelectedItem) + (char)34;
                    run_recombine(recomnbine_command);
                }
            }
            else
            {
                if (chkRPartVocal.Checked)
                {
                    input_count++; recomnbine_command += " -i " + (char)34 +
txt_output_directory.Text + @"\" + current_songname + @"\" + current_songname + @" - vocals." + codec + (char)34;
                }
                if (chkRPartBass.Checked)
                {
                    input_count++; recomnbine_command += " -i " + (char)34 +
txt_output_directory.Text + @"\" + current_songname + @"\" + current_songname + @" - bass." + codec + (char)34;
                }
                if (chkRPartDrums.Checked)
                {
                    input_count++; recomnbine_command += " -i " + (char)34 +
txt_output_directory.Text + @"\" + current_songname + @"\" + current_songname + @" - drums." + codec + (char)34;
                }
                if (chkRPartPiano.Checked)
                {
                    input_count++; recomnbine_command += " -i " + (char)34 +
txt_output_directory.Text + @"\" + current_songname + @"\" + current_songname + @" - piano." + codec + (char)34;
                }
                if (chkRPartOther.Checked)
                {
                    input_count++; recomnbine_command += " -i " + (char)34 +
txt_output_directory.Text + @"\" + current_songname + @"\" + current_songname + @" - other." + codec + (char)34;
                }
                if (recomnbine_command != "")
                {
                    String filter_a = "";
                    String filter_b = "";
                    for (int i = 0; i < input_count; i++)
                    {
                        filter_a += "[" + i + "]volume=" + input_count + "[" + ((char)97 + i) + "];";
                        filter_b += "[" + ((char)97 + i) + "]";
                    }
                    recomnbine_command = recomnbine_command + " -filter_complex " + (char)34 + filter_a + filter_b + "amix=inputs=" + input_count.ToString() + ":duration =longest"
                        + (char)34 + " -ab " + (bitrate.Value).ToString() + "k " + (char)34 + txt_output_directory.Text + @"\" + current_songname + "_recombined." + cmbBox_codec.GetItemText(cmbBox_codec.SelectedItem) + (char)34;
                    run_recombine(recomnbine_command);
                }
            }
        }

        private void ProcessExited(object sender, EventArgs e)
        {
            //called by run_cmd when thread exits after spleeting a song. runs the recombine (if enabled) and starts processing next song in queue.
            Invoke((Action)(() =>
            {
                checkForSpleeterError();

                if (chkNIStem.Checked == false && chkNIStemTwoStems.Checked == false)
                {
                    //recombine audio (if enabled)
                    if (
                                    current_songname != "" &&
                                    chkRecombine.Checked == true && (
                                    chkRPartVocal.Checked ||
                                    chkRPartBass.Checked ||
                                    chkRPartDrums.Checked ||
                                    chkRPartPiano.Checked ||
                                    chkRPartOther.Checked)
                                    )
                    {
                        recombineAudio();
                    }

                    files_remain--;
                    if (files_remain > -1)
                    {
                        //start processing the next song
                        next_song();
                    }
                    if (files_remain < 0) files_remain = 0;

                    if (!run_silent)
                    {
                        textBox1.AppendText("\r\n" + langStr["run_complete"] + "\r\n");
                        System.Media.SystemSounds.Beep.Play();
                    }
                }
                if (chkNIStem.Checked == true || chkNIStemTwoStems.Checked == true)
                // Should maybe be two different if statements if you want it to behave differently
                {
                    if (files_remain > 0)
                    {
                        NIStemRunner();

                    }
                }
            }));
        }

        private void Button2_Click(object sender, EventArgs e)
        {
            //prompt user for output folder
            var folderBrowserDialog1 = new FolderBrowserDialog();
            folderBrowserDialog1.ShowNewFolderButton = true;
            folderBrowserDialog1.Description = langStr["set_output"];
            DialogResult result = folderBrowserDialog1.ShowDialog();
            if (result == DialogResult.OK)
            {
                txt_output_directory.Text = folderBrowserDialog1.SelectedPath;
                Properties.Settings.Default.output_location = txt_output_directory.Text;
                Properties.Settings.Default.Save();
            }
            else
            {
                txt_output_directory.Text = "";
            }
        }
        private void setPythonPathToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            //prompt user for python path
            var folderBrowserDialog1 = new FolderBrowserDialog();
            folderBrowserDialog1.SelectedPath = storage;
            folderBrowserDialog1.Description = langStr["set_python_path"];
            folderBrowserDialog1.ShowNewFolderButton = false;
            DialogResult result = folderBrowserDialog1.ShowDialog();
            if (result == DialogResult.OK)
            {
                path_python = folderBrowserDialog1.SelectedPath;
                Properties.Settings.Default.path_python = path_python;
                Properties.Settings.Default.Save();
                LoadStuff();
            }
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        //private string get_config_string()
        //{
        //    //reads the JSON config file for the current stem mode
        //    var enviroment = System.Environment.CurrentDirectory;
        //    string readText = File.ReadAllText(enviroment + @"\configs\" + stem_count + "stems.json");
        //    if (mask_extension == "average")
        //    {
        //        readText = readText.Replace("zeros", "average");
        //    }
        //    return readText;
        //}

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            //sets the full bandwidth mode (16Khz)
            mask_extension = chkFullBandwidth.Checked ? "average" : "zeros";
        }

        private void spleeterGithubPageToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //help - opens SpleeterGUI github page in a browser window
            System.Diagnostics.Process.Start("https://github.com/thooore/SpleeterGUI");
        }

        private void makenItSoToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            //help - opens the Maken it so old SpleeterGUI github in a browser window
            System.Diagnostics.Process.Start("https://github.com/boy1dr/SpleeterGUI");
        }

        private void helpFAQToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //help - opens the SpleeterGUI help page in a browser window
            System.Diagnostics.Process.Start("https://github.com/thooore/SpleeterGUI/wiki");
        }

        private void parts_btn2_Click(object sender, EventArgs e)
        {
            //set the stem mode to 2
            parts_label.Text = langStr["vocal_accompaniment"];
            parts_btn2.UseVisualStyleBackColor = false;
            parts_btn4.UseVisualStyleBackColor = true;
            parts_btn5.UseVisualStyleBackColor = true;
            stem_count = "2";
            update_checks();
        }

        private void parts_btn4_Click(object sender, EventArgs e)
        {
            //set the stem mode to 4
            parts_label.Text = langStr["vocal_bass_drums_other"];
            parts_btn2.UseVisualStyleBackColor = true;
            parts_btn4.UseVisualStyleBackColor = false;
            parts_btn5.UseVisualStyleBackColor = true;
            stem_count = "4";
            update_checks();
        }

        private void parts_btn5_Click(object sender, EventArgs e)
        {
            //set the stem mode to 5
            parts_label.Text = langStr["vocal_bass_drums_piano_other"];
            parts_btn2.UseVisualStyleBackColor = true;
            parts_btn4.UseVisualStyleBackColor = true;
            parts_btn5.UseVisualStyleBackColor = false;
            stem_count = "5";
            update_checks();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            //choose a song(s) to spleet
            if (files_remain == 0)
            {
                openFileDialog2.ShowDialog();
            }
            else
            {
                System.Media.SystemSounds.Asterisk.Play();
            }
        }

        private void openFileDialog2_FileOk(object sender, CancelEventArgs e)
        {
            //files chosen, start spleeting
            if (files_remain == 0)
            {
                if (txt_output_directory.Text == "")
                {
                    MessageBox.Show(langStr["output_message"]);
                    return;
                }
                textBox1.Text = "";
                files_remain = 0;
                foreach (String file in openFileDialog2.FileNames)
                {
                    files_to_process.Add(file);
                    files_remain++;
                }
                textBox1.AppendText(langStr["starting_all"] + "\r\n");
                progressBar1.Maximum = files_remain + 1;
                progressBar1.Value = 0;
                progress_txt.Text = langStr["starting"] + "..." + files_remain + " " + langStr["songs_remaining"];
                next_song();
            }
        }

        private void spleeterupgradeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //help - spleeter core upgrade
            run_silent = false;
            current_songname = "";
            textBox1.Text = langStr["run_update"] + "\r\n" + langStr["run_update_b"] + "\r\n\r\n";
            run_cmd("pip install --upgrade spleeter");
        }

        private void checkSpleeterGUIUpdateToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //help - check SpleeterGUI version
            try
            {
                WebRequest request = WebRequest.Create("https://raw.githubusercontent.com/thooore/SpleeterGUI/master/SpleeterGui/Properties/AssemblyInfo.cs");
                WebResponse response = request.GetResponse();
                Stream data = response.GetResponseStream();
                string html = String.Empty;
                int posStart = 0;
                int posEnd = 0;
                String version_check = "";
                using (StreamReader sr = new StreamReader(data))
                {
                    html = sr.ReadToEnd();
                }
                if (html != "")
                {
                    posStart = html.IndexOf("\n[assembly: AssemblyVersion(");
                    if (posStart > 0)
                    {
                        posStart += 29;
                        posEnd = html.IndexOf('"', posStart);
                        if (posEnd > 0)
                        {
                            version_check = html.Substring(posStart, posEnd - posStart);
                            if (version_check != "" && version_check != gui_version)
                            {
                                MessageBox.Show(langStr["version"] + " " + version_check + " " + langStr["is_available"] + "\n" + langStr["current_version"] + " " + gui_version);
                            }
                            else
                            {
                                MessageBox.Show(langStr["latest"] + " " + gui_version);
                            }
                        }
                    }
                }
                else
                {
                    MessageBox.Show(langStr["unable"] + "\n" + langStr["current_version"] + " " + gui_version);
                }
            }
            catch
            {
                MessageBox.Show(langStr["unable"] + "\n" + langStr["current_version"] + " " + gui_version);
            }
        }

        private void chkRecombine_CheckedChanged(object sender, EventArgs e)
        {
            update_checks();
        }

        private void update_checks()
        {
            //update the user interface based on the chosen stem count
            chkRPartVocal.Checked = false;
            chkRPartBass.Checked = false;
            chkRPartDrums.Checked = false;
            chkRPartPiano.Checked = false;
            chkRPartOther.Checked = false;

            //          || chkSongName.Checked == true
            if (stem_count == "2" || chkNIStem.Checked == true)
            {
                chkRecombine.Checked = false;
                chkRecombine.Enabled = false;
                pnlRecombine.Height = 20;
                pnlMain.Location = new Point(12, 182);
                this.Height = 802;
                // Project height default in Designer: 667 (before)
                // this.Height = 677;
            }
            else
            {
                chkRecombine.Enabled = true;

                if (chkRecombine.Checked)
                {
                    pnlRecombine.Height = 50;
                    pnlMain.Location = new Point(12, 202);
                    this.Height = 822;
                    // this.Height = 697; (before)
                }
                else
                {
                    pnlRecombine.Height = 20;
                    pnlMain.Location = new Point(12, 182);
                    this.Height = 802;
                    // this.Height = 677; (before)

                    chkRPartVocal.Checked = false;
                    chkRPartBass.Checked = false;
                    chkRPartDrums.Checked = false;
                    chkRPartPiano.Checked = false;
                    chkRPartOther.Checked = false;
                }
                switch (stem_count)
                {
                    case "4":
                        chkRPartVocal.Enabled = true;
                        chkRPartBass.Enabled = true;
                        chkRPartDrums.Enabled = true;
                        chkRPartPiano.Enabled = false;
                        chkRPartOther.Enabled = true;
                        break;
                    case "5":
                        chkRPartVocal.Enabled = true;
                        chkRPartBass.Enabled = true;
                        chkRPartDrums.Enabled = true;
                        chkRPartPiano.Enabled = true;
                        chkRPartOther.Enabled = true;
                        break;
                }
            }
            if (chkUpdateCollection.Checked && (chkNIStem.Checked || chkNIStemTwoStems.Checked))
            {
                chkOverwriteCollection.Enabled = true;
            }
            else
            {
                chkOverwriteCollection.Enabled = false;
            }
            if (chkNIStemTwoStems.Checked || chkNIStem.Checked)
            {
                chkStemRemoveFiles.Enabled = true;
                chkStemsFolder.Enabled = true;
                chkUpdateCollection.Enabled = true;
            }
            else
            {
                chkStemRemoveFiles.Enabled = false;
                chkStemsFolder.Enabled = false;
                chkUpdateCollection.Enabled = false;
            }
        }

        private void duration_ValueChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.duration = Convert.ToInt32(duration.Value);
            Properties.Settings.Default.Save();
        }

        private void chkSongName_CheckedChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.songName = chkSongName.Checked;
            Properties.Settings.Default.Save();

            //EMPTY!!!!
            //update_checks();
        }

        private void chkNIStemTwoStems_CheckedChanged(object sender, EventArgs e)
        {

            if (chkNIStemTwoStems.Checked == true)
            {
                chkNIStem.Checked = false;
                chkRecombine.Enabled = false;
                chkSongName.Enabled = false;
                chkSongName.Checked = true;     // Probably not necessary
                cmbBox_codec.SelectedIndex = 3; // Set codec m4a
                cmbBox_codec.Enabled = false;

                parts_btn2.Enabled = false;
                parts_btn4.Enabled = false;
                parts_btn5.Enabled = false;

                //set the stem mode to 2
                parts_label.Text = langStr["vocal_accompaniment"];
                parts_btn2.UseVisualStyleBackColor = false;
                parts_btn4.UseVisualStyleBackColor = true;
                parts_btn5.UseVisualStyleBackColor = true;
                stem_count = "2";


            }
            else
            {
                //chkRecombine.Enabled = true;
                chkSongName.Enabled = true;
                cmbBox_codec.Enabled = true;
                parts_btn2.Enabled = true;
                parts_btn4.Enabled = true;
                parts_btn5.Enabled = true;
            }
            update_checks();
        }

        private void chkNIStem_CheckedChanged(object sender, EventArgs e)
        {
            update_checks();
            if (chkNIStem.Checked == true)
            {
                chkNIStemTwoStems.Checked = false;
                chkRecombine.Enabled = false;
                chkSongName.Enabled = false;
                chkSongName.Checked = true;     // Probably not necessary
                cmbBox_codec.SelectedIndex = 3; // Set codec m4a
                cmbBox_codec.Enabled = false;

                parts_btn2.Enabled = false;
                parts_btn4.Enabled = false;
                parts_btn5.Enabled = false;

                //set the stem mode to 4
                parts_label.Text = langStr["vocal_bass_drums_other"];
                parts_btn2.UseVisualStyleBackColor = true;
                parts_btn4.UseVisualStyleBackColor = false;
                parts_btn5.UseVisualStyleBackColor = true;
                stem_count = "4";


            }
            else
            {
                //chkRecombine.Enabled = true;
                chkSongName.Enabled = true;
                cmbBox_codec.Enabled = true;
                parts_btn2.Enabled = true;
                parts_btn4.Enabled = true;
                parts_btn5.Enabled = true;
            }
        }

        private void NIStemRunner()
        {
            run_ffmpeg(current_song);
        }

        private void run_NIStem()
        {

            if (File.Exists(storage + @"\ni-stem\ni-stem.exe"))
            {
                String outputArgument;

                if (!chkStemsFolder.Checked)
                {
                    outputArgument = (char)34 + txt_output_directory.Text + @"\" + current_songname + ".stem." +
                    cmbBox_codec.GetItemText(cmbBox_codec.SelectedItem) + (char)34;
                }
                else
                {
                    outputArgument = (char)34 + txt_output_directory.Text + @"\" + "stems" + @"\" + current_songname + ".stem." +
                    cmbBox_codec.GetItemText(cmbBox_codec.SelectedItem) + (char)34;
                }



                String args = "create -x " + (char)34 + txt_output_directory.Text + @"\" + current_songname + @"\" + current_songname + " - mix.wav" + (char)34 + " -s " +
                    (char)34 + txt_output_directory.Text + @"\" + current_songname + @"\" + current_songname + " - vocals." + cmbBox_codec.GetItemText(cmbBox_codec.SelectedItem) + (char)34 + " " +
                    (char)34 + txt_output_directory.Text + @"\" + current_songname + @"\" + current_songname + " - drums." + cmbBox_codec.GetItemText(cmbBox_codec.SelectedItem) + (char)34 + " " +
                    (char)34 + txt_output_directory.Text + @"\" + current_songname + @"\" + current_songname + " - bass." + cmbBox_codec.GetItemText(cmbBox_codec.SelectedItem) + (char)34 + " " +
                    (char)34 + txt_output_directory.Text + @"\" + current_songname + @"\" + current_songname + " - other." + cmbBox_codec.GetItemText(cmbBox_codec.SelectedItem) + (char)34 + " " +
                    "-m " + (char)34 + storage + @"\ni-stem\ni-stem-metadata.json" + (char)34 + " -o " + outputArgument;



                ProcessStartInfo processStartInfo = new ProcessStartInfo(storage + @"\ni-stem\ni-stem.exe", args);
                processStartInfo.WorkingDirectory = storage;

                processStartInfo.UseShellExecute = false;
                processStartInfo.ErrorDialog = false;
                processStartInfo.RedirectStandardOutput = true;
                processStartInfo.RedirectStandardError = true;
                processStartInfo.CreateNoWindow = true;
                Process process = new Process();
                process.StartInfo = processStartInfo;
                process.EnableRaisingEvents = true;
                process.Exited += new EventHandler(run_niStemExited);
                process.OutputDataReceived += new DataReceivedEventHandler(OutputHandler);
                process.ErrorDataReceived += new DataReceivedEventHandler(ErrorHandler);
                bool processStarted = process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
            }
            else
            {
                textBox1.AppendText("\r\n" +
                    "=============================" + "\r\n" +
                    "Error: ni-stem.exe not found!" + "\r\n" +
                    "=============================" + "\r\n" +
                    "You need to reinstall SpleeterCore to fix this!" + "\r\n" +
                    "Follow the install instructions under 'Help' > 'Help and FAQ'" + "\r\n" +
                    "https://github.com/thooore/SpleeterGUI/wiki" + "\r\n");
                System.Media.SystemSounds.Exclamation.Play();
                MessageBox.Show("Error: ni-stem.exe not found!" + "\r\n" +
                    "You need to reinstall SpleeterCore to fix this!" + "\r\n" +
                    "Follow the install instructions under 'Help' > 'Help and FAQ'");
            }
        }

        private void run_ffmpeg(String filename)
        {
            run_ffmpegAudio(filename);
        }

        private void run_ffmpegAudio(String filename)
        {
            String args = "-y -i " + (char)34 + filename + (char)34 + " " + (char)34 + txt_output_directory.Text + @"\" + current_songname + @"\" + current_songname + " - mix.wav" + (char)34;

            ProcessStartInfo processStartInfo = new ProcessStartInfo(storage + @"\ffmpeg.exe", args);
            processStartInfo.WorkingDirectory = storage;

            processStartInfo.UseShellExecute = false;
            processStartInfo.ErrorDialog = false;
            processStartInfo.RedirectStandardOutput = true;
            processStartInfo.RedirectStandardError = true;
            processStartInfo.CreateNoWindow = true;
            Process process = new Process();
            process.StartInfo = processStartInfo;
            process.EnableRaisingEvents = true;
            process.Exited += new EventHandler(run_ffmpegExited);
            process.OutputDataReceived += new DataReceivedEventHandler(OutputHandler);
            process.ErrorDataReceived += new DataReceivedEventHandler(ErrorHandler);
            bool processStarted = process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            textBox1.AppendText("\r\n" + ("AUDIO DONE!") + "\r\n");
        }

        private void run_ffmpegTwoStemMakerRunner(String filename)
        {
            run_ffmpegSilenceMakerDrums(filename);
            silenceMakerBass(filename);
            twoStemMakerOther(filename);
        }

        private void run_ffmpegSilenceMakerDrums(String filename)
        {
            String args = "-y -i " + (char)34 + txt_output_directory.Text + @"\" + current_songname + @"\" + current_songname + " - mix.wav" + (char)34
                + " -filter:a \"volume=0\" " + (char)34 + txt_output_directory.Text + @"\" + current_songname + @"\" + current_songname + " - drums." +
                cmbBox_codec.GetItemText(cmbBox_codec.SelectedItem) + (char)34;

            ProcessStartInfo processStartInfo = new ProcessStartInfo(storage + @"\ffmpeg.exe", args);
            processStartInfo.WorkingDirectory = storage;

            processStartInfo.UseShellExecute = false;
            processStartInfo.ErrorDialog = false;
            processStartInfo.RedirectStandardOutput = true;
            processStartInfo.RedirectStandardError = true;
            processStartInfo.CreateNoWindow = true;
            Process process = new Process();
            process.StartInfo = processStartInfo;
            process.EnableRaisingEvents = true;
            process.Exited += new EventHandler(run_doNothingOnExit);
            process.OutputDataReceived += new DataReceivedEventHandler(OutputHandler);
            process.ErrorDataReceived += new DataReceivedEventHandler(ErrorHandler);
            bool processStarted = process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();

            textBox1.AppendText("\r\n" + ("DRUMS SILENCE DONE!") + "\r\n");
        }

        private void silenceMakerBass(String filename)
        {
            File.Copy(txt_output_directory.Text + @"\" + current_songname + @"\" + current_songname + " - drums." + cmbBox_codec.GetItemText(cmbBox_codec.SelectedItem),
            txt_output_directory.Text + @"\" + current_songname + @"\" + current_songname + " - bass." + cmbBox_codec.GetItemText(cmbBox_codec.SelectedItem));

            textBox1.AppendText("\r\n" + ("BASS SILENCE DONE!") + "\r\n");
        }

        private void twoStemMakerOther(String filename)
        {
            if (File.Exists(txt_output_directory.Text + @"\" + current_songname + @"\" + current_songname + " - other." + cmbBox_codec.GetItemText(cmbBox_codec.SelectedItem)))
            {
                File.Delete(txt_output_directory.Text + @"\" + current_songname + @"\" + current_songname + " - other." + cmbBox_codec.GetItemText(cmbBox_codec.SelectedItem));
            }


            File.Move(
                txt_output_directory.Text + @"\" + current_songname + @"\" + current_songname + " - accompaniment." + cmbBox_codec.GetItemText(cmbBox_codec.SelectedItem),
                txt_output_directory.Text + @"\" + current_songname + @"\" + current_songname + " - other." + cmbBox_codec.GetItemText(cmbBox_codec.SelectedItem));

            textBox1.AppendText("\r\n" + ("RENAME ACCOMPANIMENT DONE!") + "\r\n");
        }

        private void run_StemSyncer()
        {

            if (File.Exists(storage + @"\StemSyncer\StemSyncer\StemSyncer.py"))
            {
                String collectionPath = txt_collection_path.Text;
                String outputCollectionPath = txt_collection_path_out.Text;
                if (File.Exists(collectionPath))
                {
                    string createNewCollection = "";
                    if (!(File.Exists(outputCollectionPath)))
                    {
                        textBox1.AppendText("\r\n" +
                        "Output collection does not exist! \n Trying to create a new collection!");
                        createNewCollection = " -create";
                    }
                    if (!(stemSyncerBackup))
                    {
                        File.Copy(collectionPath, txt_output_directory.Text + "\\collection_backup.nml", true);
                        stemSyncerBackup = true;
                    }
                    if (chkOverwriteCollection.Checked)
                    {
                        createNewCollection = " -create";
                    }
                    collectionPath = (char)34 + collectionPath + (char)34;
                    outputCollectionPath = (char)34 + outputCollectionPath + (char)34;

                    textBox1.AppendText("\r\n" +
                        "Running StemSyncer!");

                    String outputArgument;

                    if (!chkStemsFolder.Checked)
                    {
                        outputArgument = (char)34 + txt_output_directory.Text + @"\" + current_songname + ".stem." +
                        cmbBox_codec.GetItemText(cmbBox_codec.SelectedItem) + (char)34;
                    }
                    else
                    {
                        outputArgument = (char)34 + txt_output_directory.Text + @"\" + "stems" + @"\" + current_songname + ".stem." +
                        cmbBox_codec.GetItemText(cmbBox_codec.SelectedItem) + (char)34;
                    }

                    String args = storage + @"\StemSyncer\StemSyncer\StemSyncer.py " + " \"" + current_song + "\" " + outputArgument + " " + collectionPath + " " + outputCollectionPath + " " + createNewCollection;
                    ProcessStartInfo processStartInfo = new ProcessStartInfo(((char)34 + storage + @"\StemSyncer\python.exe" + (char)34), args);
                    processStartInfo.WorkingDirectory = storage;

                    processStartInfo.UseShellExecute = false;
                    processStartInfo.ErrorDialog = false;
                    processStartInfo.RedirectStandardOutput = true;
                    processStartInfo.RedirectStandardError = true;
                    processStartInfo.CreateNoWindow = true;
                    Process process = new Process();
                    process.StartInfo = processStartInfo;
                    process.EnableRaisingEvents = true;
                    process.Exited += new EventHandler(run_StemSyncerExited);
                    process.OutputDataReceived += new DataReceivedEventHandler(OutputHandler);
                    process.ErrorDataReceived += new DataReceivedEventHandler(ErrorHandler);
                    bool processStarted = process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    process.WaitForExit(); 
                }
                else
                {
                    textBox1.AppendText("\r\n" +
                        "Collection was not found!" + "\r\n");
                }
            }
            else
            {
                textBox1.AppendText("\r\n" +
                    "StemSyncer was not found!" + "\r\n");
            }
        }


        private void cmbBox_codec_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cmbBox_codec.SelectedIndex == 0 || cmbBox_codec.SelectedIndex == 5)
            {
                bitrate.Enabled = false;
            }
            else
            {
                bitrate.Enabled = true;
            }
            Properties.Settings.Default.codec = cmbBox_codec.SelectedIndex;
            Properties.Settings.Default.Save();
        }

        private void bitrate_ValueChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.bitrate = Convert.ToInt32(bitrate.Value);
            Properties.Settings.Default.Save();
        }

        private void btn_browse_collection_Click(object sender, EventArgs e)
        {
            //choose a song(s) to spleet
            if (files_remain == 0)
            {
                DialogResult result = openFileDialogCollection.ShowDialog();
                if (result == DialogResult.OK)
                {
                    txt_collection_path.Text = openFileDialogCollection.FileName;
                    Properties.Settings.Default.collection_location = txt_collection_path.Text;
                    Properties.Settings.Default.Save();
                }
            }
            else
            {
                System.Media.SystemSounds.Asterisk.Play();
            }
        }

        private void btn_browse_collection_out_Click(object sender, EventArgs e)
        {
            //choose a song(s) to spleet
            if (files_remain == 0)
            {
                DialogResult result = openFileDialogCollection.ShowDialog();
                if (result == DialogResult.OK)
                {
                    txt_collection_path_out.Text = openFileDialogCollection.FileName;
                    Properties.Settings.Default.collection_out_location = txt_collection_path_out.Text;
                    Properties.Settings.Default.Save();
                }
            }
            else
            {
                System.Media.SystemSounds.Asterisk.Play();
            }

        }

        private void chkUpdateCollection_CheckedChanged(object sender, EventArgs e)
        {
            update_checks();
        }
    }
}
