using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Tesseract;
using System.Windows.Forms;

namespace TemTacO
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        //Global Variables
        List<TemTem> TemTems = new List<TemTem>();
        List<TemTrait> TemTraits = new List<TemTrait>();
        List<OCR> TemResolutions = new List<OCR>();
        List<string> SupportedResolutions = new List<string>();
        OCR ResolutionSettings = new OCR();
        TemTem TemLeft = new TemTem();
        TemTem TemRight = new TemTem();
        bool TemTypeDef = false;
        bool AlwaysShowDefense = Properties.Settings.Default.AlwaysShowDefense;
        bool AlwaysShowTrait = Properties.Settings.Default.AlwaysShowTrait;

        //Logging
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public MainWindow()
        {
            InitializeComponent();
            //Fill lists with data from CSVs
            TemTems = PopulateList();
            TemTraits = PopulateTraits();
            TemResolutions = PopulateResolutions();
            SupportedResolutions = PopulateSupportedResolutions();
            //Resolution settings
            if (HandleResolution())
            {
                //Start the screen checking function if the resolution is supported.
                StartScreenChecker();
            }

            //Init Type Defense
            if (AlwaysShowDefense)
            {
                TemTacOverlay.BeginStoryboard((Storyboard)this.Resources["TypeDefenseShow"]);
                TemTypeDef = true;
            }
        }

        private void StartScreenChecker()
        {
            //DispatcherTimer setup
            DispatcherTimer dispatcherTimer = new System.Windows.Threading.DispatcherTimer();
            dispatcherTimer.Tick += new EventHandler(dispatcherTimer_Tick);
            //Run the function every 3 seconds.
            dispatcherTimer.Interval = new TimeSpan(0, 0, 1);
            dispatcherTimer.Start();
            log.Info("Started scanning");

        }

        private void dispatcherTimer_Tick(object sender, EventArgs e)
        {
            //AVOID UAC Crash
            try
            {
                //Scan Screen for Tems
                ScanScreenTem(false);
            }
            catch (Exception ex)
            {
                log.Error(ex.Message);
                log.Error(ex.StackTrace);
            }

            //Force the CommandManager to raise the RequerySuggested event
            CommandManager.InvalidateRequerySuggested();
        }        

        private void ScanScreenTem(bool save)
        {
            //Init
            Bitmap memoryImageLeft;
            memoryImageLeft = new Bitmap(ResolutionSettings.SnipW, ResolutionSettings.SnipH);
            System.Drawing.Size sL = new System.Drawing.Size(ResolutionSettings.SnipW, ResolutionSettings.SnipH);

            Graphics memoryGraphicsLeft = Graphics.FromImage(memoryImageLeft);

            //Scan TemTem Left
            memoryGraphicsLeft.CopyFromScreen(ResolutionSettings.TemLeftX, ResolutionSettings.TemLeftY, 0, 0, sL);            

            //Tesseract OCR
            memoryImageLeft = OCR.Whitify(memoryImageLeft);
            string temOCR = OCR.Tesseract(memoryImageLeft);
            //log.Info($"FoundOCR-L:{temOCR}");
            temOCR = temOCR.Split(' ')[0];
            temOCR = new String(temOCR.Where(Char.IsLetter).ToArray());
            int temOCRindex = TemTems.FindIndex(x => x.Name.Contains(temOCR));

            //Set left Tem label text
            if (!OCR.ScanForMenu() || (EnemyTemLeft.Content.ToString() != temOCR && temOCR != "" && temOCRindex > 0))
            {
                if (TemValid(temOCR))
                {
                    EnemyTemLeft.Content = temOCR;
                }
            }

            //If we found a tem update the table
            if (EnemyTemLeft.Content.ToString() != "")
            {
                //Get Tem Details
                TemLeft = GetMatchup(EnemyTemLeft.Content.ToString());

                //Get Type Defense
                LMNeutral.Content = TemLeft.TypeNeutral.ToString().TrimStart(new Char[] { '0' });
                LMFire.Content = TemLeft.TypeFire.ToString().TrimStart(new Char[] { '0' });
                LMWater.Content = TemLeft.TypeWater.ToString().TrimStart(new Char[] { '0' });
                LMNature.Content = TemLeft.TypeNature.ToString().TrimStart(new Char[] { '0' });
                LMElectric.Content = TemLeft.TypeElectric.ToString().TrimStart(new Char[] { '0' });
                LMEarth.Content = TemLeft.TypeEarth.ToString().TrimStart(new Char[] { '0' });
                LMMental.Content = TemLeft.TypeMental.ToString().TrimStart(new Char[] { '0' });
                LMWind.Content = TemLeft.TypeWind.ToString().TrimStart(new Char[] { '0' });
                LMDigital.Content = TemLeft.TypeDigital.ToString().TrimStart(new Char[] { '0' });
                LMMelee.Content = TemLeft.TypeMelee.ToString().TrimStart(new Char[] { '0' });
                LMCrystal.Content = TemLeft.TypeCrystal.ToString().TrimStart(new Char[] { '0' });
                LMToxic.Content = TemLeft.TypeToxic.ToString().TrimStart(new Char[] { '0' });

                //Add Green/Red background color
                AddColor(LeftMatchup.Children);
                LeftMatchup.Visibility = Visibility.Visible;
            }
            else
            {
                LeftMatchup.Visibility = Visibility.Collapsed;
            }

            //Init
            Bitmap memoryImageRight;
            memoryImageRight = new Bitmap(ResolutionSettings.SnipW, ResolutionSettings.SnipH);
            System.Drawing.Size sR = new System.Drawing.Size(ResolutionSettings.SnipW, ResolutionSettings.SnipH);

            Graphics memoryGraphicsRight = Graphics.FromImage(memoryImageRight);

            //Scan TemTem Right
            memoryGraphicsRight.CopyFromScreen(ResolutionSettings.TemRightX, ResolutionSettings.TemRightY, 0, 0, sR);

            //Tesseract OCR
            memoryImageRight = OCR.Whitify(memoryImageRight);
            //string fileName = string.Format(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) +
            //    @"\TemTem\" +
            //    DateTime.Now.ToString("(dd_MMMM_hh_mm_ss_tt)") + "R.png");
            //memoryImageRight.Save(fileName);
            temOCR = OCR.Tesseract(memoryImageRight);
            //log.Info($"FoundOCR-R:{temOCR}");
            temOCR = temOCR.Split(' ')[0];
            temOCR = new String(temOCR.Where(Char.IsLetter).ToArray());
            temOCRindex = TemTems.FindIndex(x => x.Name.Contains(temOCR));

            //Set left Tem label text
            if (!OCR.ScanForMenu() || (EnemyTemRight.Content.ToString() != temOCR && temOCR != "" && temOCRindex > 0))
            {
                if (TemValid(temOCR))
                {
                    EnemyTemRight.Content = temOCR;
                }                
            };

            //If we found a Tem update the table
            if (EnemyTemRight.Content.ToString() != "")
            {
                //Get Tem Details
                TemRight = GetMatchup(EnemyTemRight.Content.ToString());

                //Get Type Defense
                RMNeutral.Content = TemRight.TypeNeutral.ToString().TrimStart(new Char[] { '0' });
                RMFire.Content = TemRight.TypeFire.ToString().TrimStart(new Char[] { '0' });
                RMWater.Content = TemRight.TypeWater.ToString().TrimStart(new Char[] { '0' });
                RMNature.Content = TemRight.TypeNature.ToString().TrimStart(new Char[] { '0' });
                RMElectric.Content = TemRight.TypeElectric.ToString().TrimStart(new Char[] { '0' });
                RMEarth.Content = TemRight.TypeEarth.ToString().TrimStart(new Char[] { '0' });
                RMMental.Content = TemRight.TypeMental.ToString().TrimStart(new Char[] { '0' });
                RMWind.Content = TemRight.TypeWind.ToString().TrimStart(new Char[] { '0' });
                RMDigital.Content = TemRight.TypeDigital.ToString().TrimStart(new Char[] { '0' });
                RMMelee.Content = TemRight.TypeMelee.ToString().TrimStart(new Char[] { '0' });
                RMCrystal.Content = TemRight.TypeCrystal.ToString().TrimStart(new Char[] { '0' });
                RMToxic.Content = TemRight.TypeToxic.ToString().TrimStart(new Char[] { '0' });
                //Add Green/Red Background color
                AddColor(RightMatchup.Children);
                RightMatchup.Visibility = Visibility.Visible;
            }
            else
            {
                RightMatchup.Visibility = Visibility.Collapsed;
            }

            if (!TemTypeDef && (EnemyTemLeft.Content.ToString() != "" || EnemyTemRight.Content.ToString() != "") && !AlwaysShowDefense)
            {                
                TemTacOverlay.BeginStoryboard((Storyboard)this.Resources["TypeDefenseShow"]);
                TemTypeDef = true;               
            }
            else if (TemTypeDef && (EnemyTemLeft.Content.ToString() == "" && EnemyTemRight.Content.ToString() == "") && !AlwaysShowDefense)
            {
                TemTacOverlay.BeginStoryboard((Storyboard)this.Resources["TypeDefenseHide"]);
                TemTypeDef = false;
            }            
        }

        private void AddColor(System.Windows.Controls.UIElementCollection collection)
        {
            foreach (System.Windows.Controls.Label label in collection)
            {
                if (label.Content.ToString() != "" && double.Parse(label.Content.ToString()) < 1)
                {
                    label.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(100, 211, 111, 106));
                }
                else if (label.Content.ToString() != "" && double.Parse(label.Content.ToString()) > 1)
                {
                    label.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(100, 169, 215, 157));
                }
                else
                {
                    label.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0, 0, 0, 0));
                }
            }
        }

        private void SetTrait(TemTem Tem)
        {
            string[] Traits = Tem.Trait.ToString().Split(':');
            if(Traits.Length > 0)
            {
                //Set Trait Name
                EnemyTemTraitName1.Content = Traits[0];
                //Set Trait Description
                int index = TemTraits.FindIndex(x => x.Name.Contains(Traits[0]));
                TemTrait TemTrait = TemTraits[index];
                EnemyTemTraitDescription1.Text = TemTrait.Description;
            }
            if(Traits.Length > 1)
            {
                //Set Trait Name
                EnemyTemTraitName2.Content = Traits[1];
                //Set Trait Description
                int index = TemTraits.FindIndex(x => x.Name.Contains(Traits[1]));
                TemTrait TemTrait = TemTraits[index];
                EnemyTemTraitDescription2.Text = TemTrait.Description;
            }
        }            

        private List<TemTem> PopulateList()
        {
            log.Info("Reading TemList.csv");
            List<TemTem> temTemps = File.ReadAllLines("Resources\\TemTemList.csv")
                                           .Skip(1)
                                           .Select(v => TemTem.FromCsv(v))
                                           .ToList();
            return temTemps;
        }

        private List<TemTrait> PopulateTraits()
        {
            log.Info("Reading TemTraits.csv");
            List<TemTrait> tempTemTraits = File.ReadAllLines("Resources\\TemTraits.csv")
                                           .Skip(1)
                                           .Select(v => TemTrait.FromCsv(v))
                                           .ToList();
            return tempTemTraits;
        }

        private List<OCR> PopulateResolutions()
        {
            log.Info("Reading TemResolutions.csv");
            List<OCR> tempTemResolutions = File.ReadAllLines("Resources\\TemResolutions.csv")
                                           .Skip(1)
                                           .Select(v => OCR.FromCsv(v))
                                           .ToList();
            return tempTemResolutions;
        }

        private List<string> PopulateSupportedResolutions()
        {
            log.Info("Reading Supported Resolutions");
            List<string> tempSupportedResolutions = new List<string>();
            foreach(OCR ocr in TemResolutions)
            {
                tempSupportedResolutions.Add($"{ocr.Width}x{ocr.Height}");
            }
            return tempSupportedResolutions;
        }

        private TemTem GetMatchup(string TemName)
        {
            return TemTems.Find(x => x.Name.Contains(TemName));
        }

        private bool TemValid(string foundText)
        {
            int index = TemTems.FindIndex(x => x.Name.Contains(foundText));
            if (index != -1)
                return true;
            return false;
        }

        private void EnemyTemLeft_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (TemLeft.Trait != null && EnemyTemLeft.Content.ToString() != "")
            {
                SetTrait(TemLeft);
                TemTraitsGrid.Visibility = Visibility.Visible;
            }
        }

        private void EnemyTemRight_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (TemRight.Trait != null && EnemyTemRight.Content.ToString() != "")
            {
                SetTrait(TemRight);
                TemTraitsGrid.Visibility = Visibility.Visible;
            }
        }

        private void EnemyTemRight_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            TemTraitsGrid.Visibility = Visibility.Collapsed;
        }

        private void EnemyTemLeft_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            TemTraitsGrid.Visibility = Visibility.Collapsed;
        }

        private void BtnMenu_Click(object sender, RoutedEventArgs e)
        {
            if (TemSettings.Visibility == Visibility.Collapsed)
                TemSettings.Visibility = Visibility.Visible;
            else
                TemSettings.Visibility = Visibility.Collapsed;        
        }

        private void CheckboxDefense_Checked(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.AlwaysShowDefense = true;
            Properties.Settings.Default.Save();
            AlwaysShowDefense = true;
            TemTacOverlay.BeginStoryboard((Storyboard)this.Resources["TypeDefenseShow"]);
            TemTypeDef = true;
        }

        private void CheckboxDefense_Unchecked(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.AlwaysShowDefense = false;
            Properties.Settings.Default.Save();
            AlwaysShowDefense = false;
        }

        private void CheckboxTrait_Checked(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.AlwaysShowTrait = true;
            Properties.Settings.Default.Save();
        }

        private void CheckboxTrait_Unchecked(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.AlwaysShowTrait = false;
            Properties.Settings.Default.Save();
        }

        private bool HandleResolution()
        {
            double dWidth = -1;
            double dHeight = -1;
            if (Properties.Settings.Default.Resolution == "None")
            {
                //Get Resolution
                dWidth = SystemParameters.PrimaryScreenWidth;
                dHeight = SystemParameters.PrimaryScreenHeight;
                log.Info($"Found resolution: {dWidth}x{dHeight}");
                //Check if Resolution is supported
                //var TemSettings = TemResolutions.Where(x => x.Height.Equals(dHeight) && x.Width.Equals(dWidth));
                if (SupportedResolutions.FindIndex(x => x.Equals($"{dWidth}x{dHeight}")) != -1)
                {
                    Properties.Settings.Default.Resolution = $"{dWidth}x{dHeight}";
                    Properties.Settings.Default.Save();
                    ResolutionSettings = TemResolutions.Find(x => x.Resolution.Equals($"{dWidth}x{dHeight}"));
                    ComboBoxResolution.SelectedValue = $"{dWidth}x{dHeight}";
                    return true;
                }
                else
                {
                    System.Windows.MessageBox.Show($"{dWidth}x{dHeight} is currently not supported.\nFor more information visit 'www.temporium.gg'", "TemTacO");
                    return false;
                }
            }

            string[] resolution = Properties.Settings.Default.Resolution.Split('x'); 
            dWidth = Convert.ToInt32(resolution[0]);
            dHeight = Convert.ToInt32(resolution[1]);
            log.Info($"Settings resolution: {dWidth}x{dHeight}");
            ResolutionSettings = TemResolutions.Find(x => x.Resolution.Equals($"{dWidth}x{dHeight}"));
            ComboBoxResolution.SelectedValue = $"{dWidth}x{dHeight}";
            return true;        
        }

        private void BtnQuit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ComboBoxResolution_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if(ComboBoxResolution.SelectedValue != null && ComboBoxResolution.SelectedValue.ToString() != "")
            {
                string[] resolution = ComboBoxResolution.SelectedValue.ToString().Split('x');
                log.Info($"Changed resolution: {resolution[0]}x{resolution[1]}");
                ResolutionSettings = TemResolutions.Find(x => x.Resolution.Equals($"{resolution[0]}x{resolution[1]}"));
                Properties.Settings.Default.Resolution = $"{resolution[0]}x{resolution[1]}";
                Properties.Settings.Default.Save();
            }
        }
    }
}
