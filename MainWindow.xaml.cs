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
        string TraitDisplay = Properties.Settings.Default.TraitDisplay;

        CultureInfo enEn = new CultureInfo("en-EN");

        //Logging
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public MainWindow()
        {
            InitializeComponent();

            // Load Settings
            checkboxDefense.IsChecked = AlwaysShowDefense;
            ComboBoxTraits.SelectedValue = TraitDisplay;          

            try
            {
                //Fill lists with data from CSVs
                TemTems = PopulateList();
                TemTraits = PopulateTraits();
                TemResolutions = PopulateResolutions();
                SupportedResolutions = PopulateSupportedResolutions();
            }
            catch (Exception ex)
            {
                log.Error(ex.Message);
                log.Error(ex.StackTrace);
                Close();
            }            
            
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

        private String TypeString(float typeIn)
        {
            String str = typeIn.ToString();
            str = str.TrimStart(new Char[] { '0' });
            if (str.Equals(".5") || str.Equals(",5"))
            {
                str = "½";
            }
            else if (str.Equals(".25") || str.Equals(",25"))
            {
                str = "¼";
            }
            return str;
        }

        private void ScanScreenTem(bool save)
        {
            //Scan for full left
            Bitmap memoryImageLeft = new Bitmap(ResolutionSettings.SnipW, ResolutionSettings.SnipH);
            Graphics memoryGraphicsLeft = Graphics.FromImage(memoryImageLeft);
            //Scan TemTem Left
            memoryGraphicsLeft.CopyFromScreen(ResolutionSettings.TemLeftX, ResolutionSettings.TemLeftY, 0, 0, new System.Drawing.Size(ResolutionSettings.SnipW, ResolutionSettings.SnipH));
            //Tesseract OCR
            memoryImageLeft = OCR.Whitify(memoryImageLeft);
            string temOCR = OCR.Tesseract(memoryImageLeft);
            temOCR = temOCR.Split(' ')[0];
            temOCR = Regex.Replace(temOCR, @"[^\w]*", String.Empty);
            temOCR = new String(temOCR.Where(Char.IsLetter).ToArray());
            temOCR = temOCR.ToLower();
            int temOCRindex = TemTems.FindIndex(x => x.Name.ToLower().Contains(temOCR));
            //Set left Tem label text
            if (!OCR.ScanForMenu() || (EnemyTemLeft.Content.ToString() != temOCR && temOCR != "" && temOCRindex > 0))
            {
                if (TemValid(temOCR))
                {
                    EnemyTemLeft.Content = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(temOCR);
                }
            }

            //If left scan couldn't find anything, try scanning only for half width
            if (temOCRindex <= 0)
            {
                //Scan for half left
                Bitmap memoryImageHalfLeft = new Bitmap(ResolutionSettings.SnipW / 2, ResolutionSettings.SnipH);
                Graphics memoryGraphicsHalfLeft = Graphics.FromImage(memoryImageHalfLeft);
                //Scan TemTem Left
                memoryGraphicsHalfLeft.CopyFromScreen(ResolutionSettings.TemLeftX, ResolutionSettings.TemLeftY, 0, 0, new System.Drawing.Size(ResolutionSettings.SnipW / 2, ResolutionSettings.SnipH));
                //Tesseract OCR
                memoryImageHalfLeft = OCR.Whitify(memoryImageHalfLeft);
                string temOCRHalf = OCR.Tesseract(memoryImageHalfLeft);
                temOCRHalf = temOCRHalf.Split(' ')[0];
                temOCRHalf = Regex.Replace(temOCRHalf, @"[^\w]*", String.Empty);
                temOCRHalf = new String(temOCRHalf.Where(Char.IsLetter).ToArray());
                temOCRHalf = temOCRHalf.ToLower();
                int temOCRindexHalf = TemTems.FindIndex(x => x.Name.ToLower().Contains(temOCRHalf));
                //Set left Tem label text
                if (!OCR.ScanForMenu() || (EnemyTemLeft.Content.ToString() != temOCRHalf && temOCRHalf != "" && temOCRindexHalf > 0))
                {
                    if (TemValid(temOCRHalf))
                    {
                        EnemyTemLeft.Content = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(temOCRHalf);
                    }
                }
            }

            //If we found a tem update the table
            if (EnemyTemLeft.Content.ToString() != "")
            {
                //Get Tem Details
                TemLeft = GetMatchup(EnemyTemLeft.Content.ToString());

                if (!TemLeft.Type2.Equals("None"))
                {
                    EnemyTemLeftType.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri("Resources/" + TemLeft.Type2 + ".png", UriKind.Relative));
                    EnemyTemLeftType2.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri("Resources/" + TemLeft.Type1 + ".png", UriKind.Relative));
                }
                else
                {
                    EnemyTemLeftType.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri("Resources/" + TemLeft.Type1 + ".png", UriKind.Relative));
                    EnemyTemLeftType2.Source = null;
                }

                //Get Type Defense
                LMNeutral.Content = TypeString(TemLeft.TypeNeutral);
                LMFire.Content = TypeString(TemLeft.TypeFire);
                LMWater.Content = TypeString(TemLeft.TypeWater);
                LMNature.Content = TypeString(TemLeft.TypeNature);
                LMElectric.Content = TypeString(TemLeft.TypeElectric);
                LMEarth.Content = TypeString(TemLeft.TypeEarth);
                LMMental.Content = TypeString(TemLeft.TypeMental);
                LMWind.Content = TypeString(TemLeft.TypeWind);
                LMDigital.Content = TypeString(TemLeft.TypeDigital);
                LMMelee.Content = TypeString(TemLeft.TypeMelee);
                LMCrystal.Content = TypeString(TemLeft.TypeCrystal);
                LMToxic.Content = TypeString(TemLeft.TypeToxic);

                //Add Colored background
                AddColor(LeftMatchup.Children);
                LeftMatchup.Visibility = Visibility.Visible;
                LeftType.Visibility = Visibility.Visible;

                // Trait Visibility
                if (Properties.Settings.Default.TraitDisplay == "Always")
                {
                    SetTrait(TemLeft);
                    TemTraitsGridUp.Visibility = Visibility.Visible;
                }
            }
            else
            {
                LeftMatchup.Visibility = Visibility.Collapsed;
                LeftType.Visibility = Visibility.Collapsed;

                // Trait Visibility
                if (Properties.Settings.Default.TraitDisplay == "Always")
                {
                    TemTraitsGridUp.Visibility = Visibility.Collapsed;
                }
            }

            //Scan for full right
            Bitmap memoryImageRight = new Bitmap(ResolutionSettings.SnipW, ResolutionSettings.SnipH);
            Graphics memoryGraphicsRight = Graphics.FromImage(memoryImageRight);
            //Scan TemTem Right
            memoryGraphicsRight.CopyFromScreen(ResolutionSettings.TemRightX, ResolutionSettings.TemRightY, 0, 0, new System.Drawing.Size(ResolutionSettings.SnipW, ResolutionSettings.SnipH));
            //Tesseract OCR
            memoryImageRight = OCR.Whitify(memoryImageRight);
            temOCR = OCR.Tesseract(memoryImageRight);
            //log.Info($"FoundOCR-R:{temOCR}");
            temOCR = temOCR.Split(' ')[0];
            temOCR = Regex.Replace(temOCR, @"[^\w]*", String.Empty);
            temOCR = new String(temOCR.Where(Char.IsLetter).ToArray());
            temOCR = temOCR.ToLower();
            temOCRindex = TemTems.FindIndex(x => x.Name.ToLower().Contains(temOCR));

            //Set right Tem label text
            if (!OCR.ScanForMenu() || (EnemyTemRight.Content.ToString() != temOCR && temOCR != "" && temOCRindex > 0))
            {
                if (TemValid(temOCR))
                {
                    EnemyTemRight.Content = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(temOCR);
                }
            };

            //If right scan couldn't find anything, try scanning only for half width
            if (temOCRindex <= 0)
            {
                //Scan for half right
                Bitmap memoryImageHalfRight = new Bitmap(ResolutionSettings.SnipW / 2, ResolutionSettings.SnipH);
                Graphics memoryGraphicsHalfRight = Graphics.FromImage(memoryImageHalfRight);
                //Scan TemTem Right
                memoryGraphicsHalfRight.CopyFromScreen(ResolutionSettings.TemRightX, ResolutionSettings.TemRightY, 0, 0, new System.Drawing.Size(ResolutionSettings.SnipW / 2, ResolutionSettings.SnipH));
                //Tesseract OCR
                memoryImageHalfRight = OCR.Whitify(memoryImageHalfRight);
                string temOCRHalf = OCR.Tesseract(memoryImageHalfRight);
                //log.Info($"FoundOCR-R:{temOCR}");
                temOCRHalf = temOCRHalf.Split(' ')[0];
                temOCRHalf = Regex.Replace(temOCRHalf, @"[^\w]*", String.Empty);
                temOCRHalf = new String(temOCRHalf.Where(Char.IsLetter).ToArray());
                temOCRHalf = temOCRHalf.ToLower();
                int temOCRindexHalf = TemTems.FindIndex(x => x.Name.ToLower().Contains(temOCRHalf));

                //Set right Tem label text
                if (!OCR.ScanForMenu() || (EnemyTemRight.Content.ToString() != temOCRHalf && temOCRHalf != "" && temOCRindexHalf > 0))
                {
                    if (TemValid(temOCRHalf))
                    {
                        EnemyTemRight.Content = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(temOCRHalf);
                    }
                }
            }

            //If we found a Tem update the table
            if (EnemyTemRight.Content.ToString() != "")
            {
                //Get Tem Details
                TemRight = GetMatchup(EnemyTemRight.Content.ToString());

                if (!TemRight.Type2.Equals("None"))
                {
                    EnemyTemRightType.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri("Resources/" + TemRight.Type2 + ".png", UriKind.Relative));
                    EnemyTemRightType2.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri("Resources/" + TemRight.Type1 + ".png", UriKind.Relative));
                }
                else
                {
                    EnemyTemRightType.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri("Resources/" + TemRight.Type1 + ".png", UriKind.Relative));
                    EnemyTemRightType2.Source = null;
                }

                //Get Type Defense
                RMNeutral.Content = TypeString(TemRight.TypeNeutral);
                RMFire.Content = TypeString(TemRight.TypeFire);
                RMWater.Content = TypeString(TemRight.TypeWater);
                RMNature.Content = TypeString(TemRight.TypeNature);
                RMElectric.Content = TypeString(TemRight.TypeElectric);
                RMEarth.Content = TypeString(TemRight.TypeEarth);
                RMMental.Content = TypeString(TemRight.TypeMental);
                RMWind.Content = TypeString(TemRight.TypeWind);
                RMDigital.Content = TypeString(TemRight.TypeDigital);
                RMMelee.Content = TypeString(TemRight.TypeMelee);
                RMCrystal.Content = TypeString(TemRight.TypeCrystal);
                RMToxic.Content = TypeString(TemRight.TypeToxic);

                //Add Colored Background
                AddColor(RightMatchup.Children);
                RightMatchup.Visibility = Visibility.Visible;
                RightType.Visibility = Visibility.Visible;

                // Trait Visibility
                if (Properties.Settings.Default.TraitDisplay == "Always")
                {
                    // Switch to Upper Grid if it's not in use
                    if (TemTraitsGridUp.Visibility == Visibility.Collapsed)
                    {
                        SetTrait(TemRight);
                        TemTraitsGridUp.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        SetTraitAlt(TemRight);
                        TemTraitsGridDown.Visibility = Visibility.Visible;
                    }                 
                }
            }
            else
            {
                RightMatchup.Visibility = Visibility.Collapsed;
                RightType.Visibility = Visibility.Collapsed;

                // Trait Visibility
                if (Properties.Settings.Default.TraitDisplay == "Always")
                {
                    TemTraitsGridDown.Visibility = Visibility.Collapsed;
                }
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
                // Get Value
                string str = label.Content.ToString();
                double value = 1;
                if (str.Equals("½"))
                {
                    value = 0.5;
                } else if (str.Equals("¼"))
                {
                    value = 0.25;
                } else if (!str.Equals(string.Empty))
                {
                    value = double.Parse(str);
                }

                // Check if Label is not Empty
                if (label.Content.ToString() != string.Empty)
                {
                    // Switch on Value
                    switch (value)
                    {
                        case 0.25:
                            label.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(100, 241, 138, 51));
                            break;

                        case 0.5:
                            label.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(100, 237, 221, 58));
                            break;

                        case 2:
                            label.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(100, 109, 237, 51));
                            break;

                        case 4:
                            label.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(100, 77, 176, 51));
                            break;

                        default:
                            label.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0, 0, 0, 0));
                            break;
                    }
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

        private void SetTraitAlt(TemTem Tem)
        {
            string[] Traits = Tem.Trait.ToString().Split(':');
            if (Traits.Length > 0)
            {
                //Set Trait Name
                EnemyTemTraitName1Perma.Content = Traits[0];
                //Set Trait Description
                int index = TemTraits.FindIndex(x => x.Name.Contains(Traits[0]));
                TemTrait TemTrait = TemTraits[index];
                EnemyTemTraitDescription1Perma.Text = TemTrait.Description;
            }
            if (Traits.Length > 1)
            {
                //Set Trait Name
                EnemyTemTraitName2Perma.Content = Traits[1];
                //Set Trait Description
                int index = TemTraits.FindIndex(x => x.Name.Contains(Traits[1]));
                TemTrait TemTrait = TemTraits[index];
                EnemyTemTraitDescription2Perma.Text = TemTrait.Description;
            }
        }

        private List<TemTem> PopulateList()
        {
            log.Info("Reading TemList.csv");
            List<TemTem> temTemps = File.ReadAllLines("Resources\\TemTemList.csv")
                                           .Skip(1)
                                           .Select(v => TemTem.FromCsv(v, enEn))
                                           .ToList();
            return temTemps;
        }

        private List<TemTrait> PopulateTraits()
        {
            log.Info("Reading TemTraits.csv");
            List<TemTrait> tempTemTraits = File.ReadAllLines("Resources\\TemTraits.csv")
                                           .Skip(1)
                                           .Select(v => TemTrait.FromCsv(v, enEn))
                                           .ToList();
            return tempTemTraits;
        }

        private List<OCR> PopulateResolutions()
        {
            log.Info("Reading TemResolutions.csv");
            List<OCR> tempTemResolutions = File.ReadAllLines("Resources\\TemResolutions.csv")
                                           .Skip(1)
                                           .Select(v => OCR.FromCsv(v, enEn))
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
            if (foundText.Length <= 2)
                return false;
            int index = TemTems.FindIndex(x => x.Name.ToLower().Contains(foundText.ToLower()));
            if (index != -1)
                return true;
            return false;
        }

        private void EnemyTemLeft_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (TemLeft.Trait != null && EnemyTemLeft.Content.ToString() != string.Empty && TraitDisplay == "Hover")
            {
                SetTrait(TemLeft);
                TemTraitsGridUp.Visibility = Visibility.Visible;
            }
        }

        private void EnemyTemRight_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (TemRight.Trait != null && EnemyTemRight.Content.ToString() != string.Empty && TraitDisplay == "Hover")
            {
                SetTrait(TemRight);
                TemTraitsGridUp.Visibility = Visibility.Visible;
            }
        }

        private void EnemyTemRight_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            TemTraitsGridUp.Visibility = Visibility.Collapsed;
        }

        private void EnemyTemLeft_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            TemTraitsGridUp.Visibility = Visibility.Collapsed;
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
                    System.Windows.MessageBox.Show($"{dWidth}x{dHeight} is currently not supported. \nVisit https://github.com/BLinssen/TemTacO/issues to request a resolution.'", "TemTacO");
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
            if(ComboBoxResolution.SelectedValue != null && ComboBoxResolution.SelectedValue.ToString() != string.Empty)
            {
                string[] resolution = ComboBoxResolution.SelectedValue.ToString().Split('x');
                log.Info($"Changed resolution: {resolution[0]}x{resolution[1]}");
                ResolutionSettings = TemResolutions.Find(x => x.Resolution.Equals($"{resolution[0]}x{resolution[1]}"));                
                Properties.Settings.Default.Resolution = $"{resolution[0]}x{resolution[1]}";
                Properties.Settings.Default.Save();
            }
        }

        private void ComboBoxTraits_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ComboBoxTraits.SelectedValue != null && ComboBoxTraits.SelectedValue.ToString() != string.Empty)
            {
                Properties.Settings.Default.TraitDisplay = ComboBoxTraits.SelectedValue.ToString();
                Properties.Settings.Default.Save();

                // Reset Trait Grids
                TemTraitsGridUp.Visibility = Visibility.Collapsed;
                TemTraitsGridDown.Visibility = Visibility.Collapsed;
            }
        }
    }
}
