using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using log4net;
using TemTacO.Properties;
using Color = System.Windows.Media.Color;
using Size = System.Drawing.Size;

namespace TemTacO
{
    /// <summary>
    ///     Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        //Logging
        private static readonly ILog Log =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private readonly CultureInfo enEn = new CultureInfo("en-EN");
        private readonly List<string> SupportedResolutions = new List<string>();

        private readonly List<OCR> TemResolutions = new List<OCR>();

        //Global Variables
        private readonly List<TemTem> TemTems = new List<TemTem>();
        private readonly List<TemTrait> TemTraits = new List<TemTrait>();
        private readonly string TraitDisplay = Settings.Default.TraitDisplay;
        private bool AlwaysShowDefense = Settings.Default.AlwaysShowDefense;
        private OCR ResolutionSettings = new OCR();
        private TemTem TemLeft = new TemTem();
        private TemTem TemRight = new TemTem();
        private bool TemTypeDef;

        private Timer ScreenScanTimer;

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
                Log.Error(ex.Message);
                Log.Error(ex.StackTrace);
                Close();
            }

            //Resolution settings
            if (HandleResolution())
            {
                //Start the screen checking and progress bars if the resolution is supported.
                StartScreenChecker();
                StartProgressBar(Type.Left);
                StartProgressBar(Type.Right);
            }

            //Init Type Defense
            if (AlwaysShowDefense)
            {
                TemTacOverlay.BeginStoryboard((Storyboard)Resources["TypeDefenseShow"]);
                TemTypeDef = true;
            }
        }

        private void StartScreenChecker()
        {
            //We run this on a separate thread to prevent the UI thread being locked up while it executes
            ScreenScanTimer = new Timer(new TimerCallback(ScanScreenTimer), new AutoResetEvent(false), 1000, 1000);
            Log.Info("Started scanning");
        }

        private void ScanScreenTimer(object state)
        {
            //Avoid user account control crash
            try
            {
                ScanScreenForTemtems();
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);
                Log.Error(ex.StackTrace);
            }

            //Force the CommandManager to raise the RequerySuggested event
            CommandManager.InvalidateRequerySuggested();
        }

        private void StartProgressBar(Type type)
        {
            var dispatcherTimer = new DispatcherTimer();
            dispatcherTimer.Tick += (sender, e) => { ProgressTimer_Tick(type); };
            //We run every 15 milliseconds so the progress bar can run at up to 60hz
            //It would need to run more frequently for higher hertz, but 60 should be more than enough
            //Running every 15 milliseconds may be too taxing on the system though, so this might need to be made less frequent
            dispatcherTimer.Interval = new TimeSpan(0, 0, 0, 0, 15);
            dispatcherTimer.Start();
            Log.Info("Started " + type + " progress bar cycle");
        }

        private void ProgressTimer_Tick(Type type)
        {
            try
            {
                switch (type)
                {
                    case Type.Left:
                        //Decrement value by 0.2 if it's visible
                        if (LeftProgressBar.IsVisible)
                            LeftProgressBar.Value -= 0.175;

                        //If the progress bar has reached 0, hide it and reset the text
                        if (LeftProgressBar.Value <= 0)
                        {
                            LeftProgressBar.Visibility = Visibility.Collapsed;
                            EnemyTemLeft.Content = "";
                        }
                        //Otherwise, make sure it's visible
                        else if (LeftProgressBar.Value > 0 && !LeftProgressBar.IsVisible)
                        {
                            LeftProgressBar.Visibility = Visibility.Visible;
                        }

                        break;
                    case Type.Right:
                        //Decrement value by 0.2 if it's visible
                        if (RightProgressBar.IsVisible)
                            RightProgressBar.Value -= 0.175;

                        //If the progress bar has reached 0, hide it and reset the text
                        if (RightProgressBar.Value <= 0)
                        {
                            RightProgressBar.Visibility = Visibility.Collapsed;
                            EnemyTemRight.Content = "";
                        }
                        //Otherwise, make sure it's visible
                        else if (RightProgressBar.Value > 0 && !RightProgressBar.IsVisible)
                        {
                            RightProgressBar.Visibility = Visibility.Visible;
                        }

                        break;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);
                Log.Error(ex.StackTrace);
            }

            //Force the CommandManager to raise the RequerySuggested event
            CommandManager.InvalidateRequerySuggested();
        }

        private string TypeString(float typeIn)
        {
            var str = typeIn.ToString();
            str = str.TrimStart('0');
            if (str.Equals(".5") || str.Equals(",5"))
                str = "½";
            else if (str.Equals(".25") || str.Equals(",25")) str = "¼";

            return str;
        }

        private void ScanScreenForTemtems()
        {
            //Scan for full left
            var memoryImageLeft = new Bitmap(ResolutionSettings.SnipW, ResolutionSettings.SnipH);
            var memoryGraphicsLeft = Graphics.FromImage(memoryImageLeft);
            //Scan TemTem Left
            memoryGraphicsLeft.CopyFromScreen(ResolutionSettings.TemLeftX, ResolutionSettings.TemLeftY, 0, 0,
                new Size(ResolutionSettings.SnipW, ResolutionSettings.SnipH));
            //Tesseract OCR
            memoryImageLeft = OCR.Whitify(memoryImageLeft);
            var temOCR = OCR.Tesseract(memoryImageLeft);
            temOCR = temOCR.Split(' ')[0];
            temOCR = Regex.Replace(temOCR, @"[^\w]*", string.Empty);
            temOCR = new string(temOCR.Where(char.IsLetter).ToArray());
            temOCR = temOCR.ToLower();
            var temOCRindex = TemTems.FindIndex(x => x.Name.ToLower().Contains(temOCR));
            //Set left Tem label text
            if (!OCR.ScanForMenu() || (EnemyTemLeft.Content.ToString() != temOCR && temOCR != "" && temOCRindex > 0))
                if (TemValid(temOCR))
                {
                    Dispatcher.Invoke(() =>
                    {
                        EnemyTemLeft.Content = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(temOCR);
                        LeftProgressBar.Value = 100;
                        LeftProgressBar.Visibility = Visibility.Visible;
                    });
                }

            //If left scan couldn't find anything, try scanning only for half width
            if (temOCRindex <= 0)
            {
                //Scan for half left
                var memoryImageHalfLeft = new Bitmap(ResolutionSettings.SnipW / 2, ResolutionSettings.SnipH);
                var memoryGraphicsHalfLeft = Graphics.FromImage(memoryImageHalfLeft);
                //Scan TemTem Left
                memoryGraphicsHalfLeft.CopyFromScreen(ResolutionSettings.TemLeftX, ResolutionSettings.TemLeftY, 0, 0,
                    new Size(ResolutionSettings.SnipW / 2, ResolutionSettings.SnipH));
                //Tesseract OCR
                memoryImageHalfLeft = OCR.Whitify(memoryImageHalfLeft);
                var temOCRHalf = OCR.Tesseract(memoryImageHalfLeft);
                temOCRHalf = temOCRHalf.Split(' ')[0];
                temOCRHalf = Regex.Replace(temOCRHalf, @"[^\w]*", string.Empty);
                temOCRHalf = new string(temOCRHalf.Where(char.IsLetter).ToArray());
                temOCRHalf = temOCRHalf.ToLower();
                var temOCRindexHalf = TemTems.FindIndex(x => x.Name.ToLower().Contains(temOCRHalf));
                //Set left Tem label text
                if (!OCR.ScanForMenu() || (EnemyTemLeft.Content.ToString() != temOCRHalf && temOCRHalf != "" &&
                                           temOCRindexHalf > 0))
                    if (TemValid(temOCRHalf))
                    {
                        Dispatcher.Invoke(() =>
                        {
                            EnemyTemLeft.Content = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(temOCRHalf);
                            LeftProgressBar.Value = 100;
                            LeftProgressBar.Visibility = Visibility.Visible;
                        });
                    }
            }

            //If we found a tem update the table

            Dispatcher.Invoke(() =>
            {
                if (EnemyTemLeft.Content != null && EnemyTemLeft.Content.ToString() != "")
                {
                    //Get Tem Details
                    TemLeft = GetMatchup(EnemyTemLeft.Content.ToString());

                    if (!TemLeft.Type2.Equals("None"))
                    {
                        EnemyTemLeftType.Source =
                            new BitmapImage(new Uri("Resources/" + TemLeft.Type2 + ".png",
                                UriKind.Relative));
                        EnemyTemLeftType2.Source =
                            new BitmapImage(new Uri("Resources/" + TemLeft.Type1 + ".png",
                                UriKind.Relative));
                    }
                    else
                    {
                        EnemyTemLeftType.Source =
                            new BitmapImage(new Uri("Resources/" + TemLeft.Type1 + ".png",
                                UriKind.Relative));
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
                    LeftProgressBar.Visibility = Visibility.Visible;

                    // Trait Visibility
                    if (Settings.Default.TraitDisplay == "Always")
                        SetTrait(TemLeft, TemTraitsGridUp, Visibility.Visible);
                }
                else
                {
                    LeftMatchup.Visibility = Visibility.Collapsed;
                    LeftType.Visibility = Visibility.Collapsed;
                    LeftProgressBar.Visibility = Visibility.Collapsed;

                    // Trait Visibility
                    if (Settings.Default.TraitDisplay == "Always") TemTraitsGridUp.Visibility = Visibility.Collapsed;
                }
            });

            //Scan for full right
            var memoryImageRight = new Bitmap(ResolutionSettings.SnipW, ResolutionSettings.SnipH);
            var memoryGraphicsRight = Graphics.FromImage(memoryImageRight);
            //Scan TemTem Right
            memoryGraphicsRight.CopyFromScreen(ResolutionSettings.TemRightX, ResolutionSettings.TemRightY, 0, 0,
                new Size(ResolutionSettings.SnipW, ResolutionSettings.SnipH));
            //Tesseract OCR
            memoryImageRight = OCR.Whitify(memoryImageRight);
            temOCR = OCR.Tesseract(memoryImageRight);
            //Log.Info($"FoundOCR-R:{temOCR}");
            temOCR = temOCR.Split(' ')[0];
            temOCR = Regex.Replace(temOCR, @"[^\w]*", string.Empty);
            temOCR = new string(temOCR.Where(char.IsLetter).ToArray());
            temOCR = temOCR.ToLower();
            temOCRindex = TemTems.FindIndex(x => x.Name.ToLower().Contains(temOCR));

            //Set right Tem label text
            if (!OCR.ScanForMenu() || (EnemyTemRight.Content.ToString() != temOCR && temOCR != "" && temOCRindex > 0))
                if (TemValid(temOCR))
                {
                    Dispatcher.Invoke(() =>
                    {
                        EnemyTemRight.Content = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(temOCR);
                        RightProgressBar.Value = 100;
                        RightProgressBar.Visibility = Visibility.Visible;
                    });
                }

            ;

            //If right scan couldn't find anything, try scanning only for half width
            if (temOCRindex <= 0)
            {
                //Scan for half right
                var memoryImageHalfRight = new Bitmap(ResolutionSettings.SnipW / 2, ResolutionSettings.SnipH);
                var memoryGraphicsHalfRight = Graphics.FromImage(memoryImageHalfRight);
                //Scan TemTem Right
                memoryGraphicsHalfRight.CopyFromScreen(ResolutionSettings.TemRightX, ResolutionSettings.TemRightY, 0, 0,
                    new Size(ResolutionSettings.SnipW / 2, ResolutionSettings.SnipH));
                //Tesseract OCR
                memoryImageHalfRight = OCR.Whitify(memoryImageHalfRight);
                var temOCRHalf = OCR.Tesseract(memoryImageHalfRight);
                //Log.Info($"FoundOCR-R:{temOCR}");
                temOCRHalf = temOCRHalf.Split(' ')[0];
                temOCRHalf = Regex.Replace(temOCRHalf, @"[^\w]*", string.Empty);
                temOCRHalf = new string(temOCRHalf.Where(char.IsLetter).ToArray());
                temOCRHalf = temOCRHalf.ToLower();
                var temOCRindexHalf = TemTems.FindIndex(x => x.Name.ToLower().Contains(temOCRHalf));

                //Set right Tem label text
                if (!OCR.ScanForMenu() || (EnemyTemRight.Content.ToString() != temOCRHalf && temOCRHalf != "" &&
                                           temOCRindexHalf > 0))
                    if (TemValid(temOCRHalf))
                    {
                        Dispatcher.Invoke(() =>
                        {
                            EnemyTemRight.Content = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(temOCRHalf);
                            RightProgressBar.Value = 100;
                            RightProgressBar.Visibility = Visibility.Visible;
                        });
                    }
            }

            //If we found a Tem update the table
            Dispatcher.Invoke(() =>
            {
                if (EnemyTemRight.Content != null && EnemyTemRight.Content.ToString() != "")
                {
                    //Get Tem Details
                    TemRight = GetMatchup(EnemyTemRight.Content.ToString());

                    if (!TemRight.Type2.Equals("None"))
                    {
                        EnemyTemRightType.Source =
                            new BitmapImage(new Uri("Resources/" + TemRight.Type2 + ".png",
                                UriKind.Relative));
                        EnemyTemRightType2.Source =
                            new BitmapImage(new Uri("Resources/" + TemRight.Type1 + ".png",
                                UriKind.Relative));
                    }
                    else
                    {
                        EnemyTemRightType.Source =
                            new BitmapImage(new Uri("Resources/" + TemRight.Type1 + ".png",
                                UriKind.Relative));
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
                    RightProgressBar.Visibility = Visibility.Visible;

                    // Trait Visibility
                    if (Settings.Default.TraitDisplay == "Always")
                    {
                        // Switch to Upper Grid if it's not in use
                        if (TemTraitsGridUp.Visibility == Visibility.Collapsed)
                            SetTrait(TemRight, TemTraitsGridUp, Visibility.Visible);
                        else
                            SetTraitAlt(TemRight, TemTraitsGridDown, Visibility.Visible);
                    }
                }
                else
                {
                    RightMatchup.Visibility = Visibility.Collapsed;
                    RightType.Visibility = Visibility.Collapsed;
                    RightProgressBar.Visibility = Visibility.Collapsed;

                    // Trait Visibility
                    if (Settings.Default.TraitDisplay == "Always") TemTraitsGridDown.Visibility = Visibility.Collapsed;
                }

                if (!TemTypeDef &&
                    ((EnemyTemLeft.Content != null && EnemyTemLeft.Content.ToString() != "") ||
                     (EnemyTemRight.Content != null && EnemyTemRight.Content.ToString() != "")) && !AlwaysShowDefense)
                {
                    TemTacOverlay.BeginStoryboard((Storyboard)Resources["TypeDefenseShow"]);
                    TemTypeDef = true;
                }
                else if (TemTypeDef && EnemyTemLeft.Content.ToString() == "" &&
                         EnemyTemRight.Content.ToString() == "" &&
                         !AlwaysShowDefense)
                {
                    TemTacOverlay.BeginStoryboard((Storyboard)Resources["TypeDefenseHide"]);
                    TemTypeDef = false;
                }
            });
        }

        private void AddColor(UIElementCollection collection)
        {
            foreach (Label label in collection)
            {
                // Get Value
                var str = label.Content.ToString();
                double value = 1;
                if (str.Equals("½"))
                    value = 0.5;
                else if (str.Equals("¼"))
                    value = 0.25;
                else if (!str.Equals(string.Empty)) value = double.Parse(str);

                // Check if Label is not Empty
                if (label.Content.ToString() != string.Empty)
                    // Switch on Value
                    switch (value)
                    {
                        case 0.25:
                            label.Background =
                                new SolidColorBrush(
                                    Color.FromArgb(100, 241, 138, 51));
                            break;

                        case 0.5:
                            label.Background =
                                new SolidColorBrush(
                                    Color.FromArgb(100, 237, 221, 58));
                            break;

                        case 2:
                            label.Background =
                                new SolidColorBrush(
                                    Color.FromArgb(100, 109, 237, 51));
                            break;

                        case 4:
                            label.Background =
                                new SolidColorBrush(
                                    Color.FromArgb(100, 77, 176, 51));
                            break;

                        default:
                            label.Background =
                                new SolidColorBrush(
                                    Color.FromArgb(0, 0, 0, 0));
                            break;
                    }
                else
                    label.Background =
                        new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
            }
        }

        private void SetTrait(TemTem Tem, Grid grid, Visibility visible)
        {
            var Traits = Tem.Trait.Split(':');
            if (Traits[0].Equals("Unknown")) return;

            if (Traits.Length > 0)
            {
                //Set Trait Name
                EnemyTemTraitName1.Content = Traits[0];
                //Set Trait Description
                var index = TemTraits.FindIndex(x => x.Name.ToLower().Contains(Traits[0].ToLower()));
                var TemTrait = TemTraits[index];
                EnemyTemTraitDescription1.Text = TemTrait.Description;
            }

            if (Traits.Length > 1)
            {
                //Set Trait Name
                EnemyTemTraitName2.Content = Traits[1];
                //Set Trait Description
                var index = TemTraits.FindIndex(x => x.Name.ToLower().Contains(Traits[1].ToLower()));
                var TemTrait = TemTraits[index];
                EnemyTemTraitDescription2.Text = TemTrait.Description;
            }

            grid.Visibility = visible;
        }

        private void SetTraitAlt(TemTem Tem, Grid grid, Visibility visible)
        {
            var Traits = Tem.Trait.Split(':');
            if (Traits[0].Equals("Unknown")) return;

            if (Traits.Length > 0)
            {
                //Set Trait Name
                EnemyTemTraitName1Perma.Content = Traits[0];
                //Set Trait Description
                var index = TemTraits.FindIndex(x => x.Name.ToLower().Contains(Traits[0].ToLower()));
                var TemTrait = TemTraits[index];
                EnemyTemTraitDescription1Perma.Text = TemTrait.Description;
            }

            if (Traits.Length > 1)
            {
                //Set Trait Name
                EnemyTemTraitName2Perma.Content = Traits[1];
                //Set Trait Description
                var index = TemTraits.FindIndex(x => x.Name.ToLower().Contains(Traits[1].ToLower()));
                var TemTrait = TemTraits[index];
                EnemyTemTraitDescription2Perma.Text = TemTrait.Description;
            }

            grid.Visibility = visible;
        }

        private List<TemTem> PopulateList()
        {
            Log.Info("Reading TemList.csv");
            var temTemps = File.ReadAllLines("Resources\\TemTemList.csv")
                .Skip(1)
                .Select(v => TemTem.FromCsv(v, enEn))
                .ToList();
            return temTemps;
        }

        private List<TemTrait> PopulateTraits()
        {
            Log.Info("Reading TemTraits.csv");
            var tempTemTraits = File.ReadAllLines("Resources\\TemTraits.csv")
                .Skip(1)
                .Select(v => TemTrait.FromCsv(v, enEn))
                .ToList();
            return tempTemTraits;
        }

        private List<OCR> PopulateResolutions()
        {
            Log.Info("Reading TemResolutions.csv");
            var tempTemResolutions = File.ReadAllLines("Resources\\TemResolutions.csv")
                .Skip(1)
                .Select(v => OCR.FromCsv(v, enEn))
                .ToList();
            return tempTemResolutions;
        }

        private List<string> PopulateSupportedResolutions()
        {
            Log.Info("Reading Supported Resolutions");
            var tempSupportedResolutions = new List<string>();
            foreach (var ocr in TemResolutions) tempSupportedResolutions.Add($"{ocr.Width}x{ocr.Height}");

            return tempSupportedResolutions;
        }

        private TemTem GetMatchup(string TemName)
        {
            return TemTems.Find(x => x.Name.ToLower().Contains(TemName.ToLower()));
        }

        private bool TemValid(string foundText)
        {
            if (foundText.Length <= 2)
                return false;
            var index = TemTems.FindIndex(x => x.Name.ToLower().Contains(foundText.ToLower()));
            if (index != -1)
                return true;
            return false;
        }

        private void EnemyTemLeft_MouseEnter(object sender, MouseEventArgs e)
        {
            if (TemLeft.Trait != null && EnemyTemLeft.Content.ToString() != string.Empty && TraitDisplay == "Hover")
                SetTrait(TemLeft, TemTraitsGridUp, Visibility.Visible);
        }

        private void EnemyTemRight_MouseEnter(object sender, MouseEventArgs e)
        {
            if (TemRight.Trait != null && EnemyTemRight.Content.ToString() != string.Empty && TraitDisplay == "Hover")
                SetTrait(TemRight, TemTraitsGridUp, Visibility.Visible);
        }

        private void EnemyTemRight_MouseLeave(object sender, MouseEventArgs e)
        {
            TemTraitsGridUp.Visibility = Visibility.Collapsed;
        }

        private void EnemyTemLeft_MouseLeave(object sender, MouseEventArgs e)
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
            Settings.Default.AlwaysShowDefense = true;
            Settings.Default.Save();
            AlwaysShowDefense = true;
            TemTacOverlay.BeginStoryboard((Storyboard)Resources["TypeDefenseShow"]);
            TemTypeDef = true;
        }

        private void CheckboxDefense_Unchecked(object sender, RoutedEventArgs e)
        {
            Settings.Default.AlwaysShowDefense = false;
            Settings.Default.Save();
            AlwaysShowDefense = false;
        }

        private bool HandleResolution()
        {
            double dWidth = -1;
            double dHeight = -1;
            if (Settings.Default.Resolution == "None")
            {
                //Get Resolution
                dWidth = SystemParameters.PrimaryScreenWidth;
                dHeight = SystemParameters.PrimaryScreenHeight;
                Log.Info($"Found resolution: {dWidth}x{dHeight}");
                //Check if Resolution is supported
                if (SupportedResolutions.FindIndex(x => x.Equals($"{dWidth}x{dHeight}")) != -1)
                {
                    Settings.Default.Resolution = $"{dWidth}x{dHeight}";
                    Settings.Default.Save();
                    ResolutionSettings = TemResolutions.Find(x => x.Resolution.Equals($"{dWidth}x{dHeight}"));
                    ComboBoxResolution.SelectedValue = $"{dWidth}x{dHeight}";
                    return true;
                }

                MessageBox.Show(
                    $"{dWidth}x{dHeight} is currently not supported. \nVisit https://github.com/BLinssen/TemTacO/issues to request a resolution.'",
                    "TemTacO");
                return false;
            }

            var resolution = Settings.Default.Resolution.Split('x');
            dWidth = Convert.ToInt32(resolution[0]);
            dHeight = Convert.ToInt32(resolution[1]);
            Log.Info($"Settings resolution: {dWidth}x{dHeight}");
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
            if (ComboBoxResolution.SelectedValue != null && ComboBoxResolution.SelectedValue.ToString() != string.Empty)
            {
                var resolution = ComboBoxResolution.SelectedValue.ToString().Split('x');
                Log.Info($"Changed resolution: {resolution[0]}x{resolution[1]}");
                ResolutionSettings = TemResolutions.Find(x => x.Resolution.Equals($"{resolution[0]}x{resolution[1]}"));
                Settings.Default.Resolution = $"{resolution[0]}x{resolution[1]}";
                Settings.Default.Save();
            }
        }

        private void ComboBoxTraits_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ComboBoxTraits.SelectedValue != null && ComboBoxTraits.SelectedValue.ToString() != string.Empty)
            {
                Settings.Default.TraitDisplay = ComboBoxTraits.SelectedValue.ToString();
                Settings.Default.Save();

                // Reset Trait Grids
                TemTraitsGridUp.Visibility = Visibility.Collapsed;
                TemTraitsGridDown.Visibility = Visibility.Collapsed;
            }
        }

        private enum Type
        {
            Left,
            Right
        }
    }
}