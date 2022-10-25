using System.ComponentModel;

namespace GenieUtilsFE
{
    public partial class MainPage : ContentPage, INotifyPropertyChanged
    {
        // destination root folder for extracted scripts and config files
        private string _outputFolder;
        public string OutputFolder
        {
            get => _outputFolder;
            set
            {
                _outputFolder = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(OutputFolder)));
            }
        }

        // Stormfront or Wrayth settings file to extract from
        private string _settingsFile;
        public string SettingsFile
        {
            get => _settingsFile; set
            {
                _settingsFile = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SettingsFile)));
            }
        }

        // If true, settings file has been successfully loaded
        private bool _settingsLoaded = false;
        public bool SettingsLoaded
        {
            get => _settingsLoaded; set 
            {
                _settingsLoaded = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SettingsLoaded)));
            }

        }

        private double _loadingProgress = 0.0;
        public double LoadingProgress { get => _loadingProgress; set 
            {
                _loadingProgress = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LoadingProgress)));
            }
        }

        private int _numScripts = 0;
        public int NumScripts
        {
            get => _numScripts; set 
            {
                _numScripts = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NumScripts)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ScriptsAvailable)));
            }
        }

        private string _processStatus;
        public string ProcessStatus
        {
            get => _processStatus; set 
            {
                _processStatus = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ProcessStatus)));
            }
        }
        public string ScriptsAvailable { get => $"Scripts ({NumScripts} available)"; }

        private bool _isNotBusy = true;
        public bool IsNotBusy
        { 
            get => _isNotBusy; set 
            {
                _isNotBusy = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsNotBusy)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCurrentlyBusy)));
                // Note: There is a known bug that the color attributes of buttons are not properly restored
                // After being disabled and re-enabled.  No known workarounds.  The buttons function properly however
            }

        }

        public bool IsCurrentlyBusy { get => !IsNotBusy; }

        public bool DoScripts { get; set; } = true;
        public bool DoOthers { get; set; } = true;

        public bool DoOverwrite { get; set; } = true;

        public SettingsExtractor Extractor; 


        public MainPage()
        {
            InitializeComponent();
            BindingContext = this;
            OutputFolder = FileSystem.Current.AppDataDirectory;
            SettingsFile = GetDefaultSettingsFile();
            SetupProgressBar();
        }

        private async void ChangeDestinationBtn_Clicked(object sender, EventArgs e)
        {
            if (IsCurrentlyBusy)
                return;
            IsNotBusy = false;
            string folder = await PickStorageFolder();
            if (!string.IsNullOrEmpty(folder))
                OutputFolder = folder;
            IsNotBusy = true;
        }

        private async Task<string> PickStorageFolder()
        {
            string storageFolder = FileSystem.Current.AppDataDirectory;
#if WINDOWS
                    var folderPicker = new GenieUtilsFE.Windows.WinFolderPicker();
           		    storageFolder = await folderPicker.PickFolder();
#endif
            return storageFolder;
        }

        private void ExtractConfigBtn_Clicked(object sender, EventArgs e)
        {
            if (IsCurrentlyBusy)
                return;
            if (Extractor == null)
            {
                ProcessStatus = "No settings file loaded";
                return;
            }
            if (!DoScripts && !DoOthers)
            {
                ProcessStatus = "Nothing to process";
                return;
            }
            ProcessSettings();
        }

        private async void ProcessSettings()
        {
            IsNotBusy = false;
            LoadingProgress = 0.0;
            await Task.Run( async () =>
            {
                if (DoScripts)
                {
                    var result = Extractor.ExtractScripts(this,OutputFolder, DoOverwrite);
                    if (result == null)
                    {
                        SettingsFile = GetDefaultSettingsFile();
                        SettingsLoaded = false;
                        ProcessStatus = "Settings file load cancelled";
                    }
                    else
                    {
                        ProcessStatus = "Scripts extracted to files";
                        if (DoOthers)
                        {
                            var configresult = Extractor.ExtractOtherElements(this, OutputFolder, DoOverwrite);
                            if (configresult == null)
                            {
                                ProcessStatus = "Configuration extraction failed";
                            }
                            else
                            {
                                ProcessStatus = "Scripts and Configuration values extracted";
                            }
                        }
                    }
                }

                // Now on background thread.
                // Report progress to UI.
                Dispatcher.Dispatch(() =>
                {
                    // Code here is queued to run on MainThread.
                    // Assuming you don't need to wait for the result,
                    // don't need await/async here.
                });
            });

            IsNotBusy = true;

        }

        private async void ChangeSourceBtn_Clicked(object sender, EventArgs e)
        {
            if (IsCurrentlyBusy)
                return;
            IsNotBusy = false;
            var result = await PickStormfrontFile(PickOptions.Default);
            if (result == null)
            {
                SettingsFile = GetDefaultSettingsFile();
                SettingsLoaded = false;
                ProcessStatus = "Settings file load cancelled";
            }
            else
            {
                SettingsLoaded = false;
                ProcessStatus = "Settings file loading";
                if (Extractor == null)
                    Extractor = new SettingsExtractor();

                var loadresult = await Extractor.LoadSettingsDoc(result);
                if (loadresult.Equals("success"))
                {
                    SettingsFile = result.FullPath;
                    NumScripts = Extractor.GetNumberScripts();
                    SettingsLoaded = true;
                    ProcessStatus = "Settings file loaded";
                }
                else if (loadresult.Equals("already loaded"))
                {
                    SettingsLoaded = true;
                    ProcessStatus = "Settings file already loaded";
                }
                else
                {
                    ProcessStatus = "Settings file loading failed";
                    NumScripts = 0;
                }

            }
            IsNotBusy = true;

        }

        private async Task<FileResult> PickStormfrontFile(PickOptions options)
        {
           
            try
            {
                var result = await FilePicker.Default.PickAsync(options);
                if (result != null)
                {
                    if (result.FileName.EndsWith("xml", StringComparison.OrdinalIgnoreCase))
                        return result;
                }
            }
            catch (Exception exc)
            {
                // The user canceled or something went wrong
            }

            return null;
        }


        private string GetDefaultSettingsFile()
        {
            string defaultFile = "";
#if WINDOWS
            // For old Stormfront was c:\Stormfront.xml
            defaultFile = @"C:\Wrayth.xml";
#endif
            return SettingsFile ?? defaultFile;
        }

        private void SetupProgressBar()
        {
        }

        public new event PropertyChangedEventHandler PropertyChanged;

    }
}