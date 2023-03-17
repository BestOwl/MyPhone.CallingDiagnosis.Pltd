#nullable enable
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.Calls;
using Windows.Devices.Enumeration;
using Windows.System;

namespace MyPhone.CallingDiagnosis.Pltd
{
    public partial class MainPageViewModel : ObservableObject, IDisposable
    {
        private readonly DispatcherQueue _dispatcherQueue;

        public ObservableCollection<PhoneLine> PhoneLines { get; }

        public ObservableCollection<DeviceInformation> PltdInfos { get; }

        public ObservableCollection<string> Logs { get; }

        private DeviceWatcher _pltdWatcher;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(RegisterAppCommand))]
        [NotifyCanExecuteChangedFor(nameof(RegisterAppForUserCommand))]
        [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
        private DeviceInformation? _selectedPltdInfo;

        [ObservableProperty]
        private bool? _pltdConnected;

        [ObservableProperty]
        private bool? _pltdAppRegistered;

        public MainPageViewModel(DispatcherQueue dispatcherQueue) 
        {
            _dispatcherQueue = dispatcherQueue;

            PhoneLines = new ObservableCollection<PhoneLine>();
            PltdInfos = new ObservableCollection<DeviceInformation>();
            Logs = new ObservableCollection<string>();

            _pltdWatcher = DeviceInformation.CreateWatcher(PhoneLineTransportDevice.GetDeviceSelector());
            _pltdWatcher.Added += _pltdWatcher_Added;
            _pltdWatcher.Removed += _pltdWatcher_Removed;
            _pltdWatcher.Start();
        }

        partial void OnSelectedPltdInfoChanged(DeviceInformation? value)
        {
            if (value != null) 
            {
                PhoneLineTransportDevice pltd = PhoneLineTransportDevice.FromId(value.Id);
                PltdAppRegistered = pltd.IsRegistered();
            }
        }
            

        [RelayCommand(CanExecute = nameof(CanRegisterApp))]
        private async Task RegisterAppAsync()
        {
            await RegisterAppInternal(true);
        }

        [RelayCommand(CanExecute = nameof(CanRegisterApp))]
        private async Task RegisterAppForUserAsync()
        {
            await RegisterAppInternal(false);
        }

        private bool CanRegisterApp()
        {
            return SelectedPltdInfo != null;
        }

        private async Task RegisterAppInternal(bool systemWide)
        {
            Trace.Assert(SelectedPltdInfo != null);
            PhoneLineTransportDevice pltd = PhoneLineTransportDevice.FromId(SelectedPltdInfo!.Id);
            DeviceAccessStatus accessStatus = await pltd.RequestAccessAsync();
            if (accessStatus == DeviceAccessStatus.Allowed)
            {
                if (systemWide)
                {
                    pltd.RegisterApp();
                }
                else
                {
                    pltd.RegisterAppForUser(App.Instance.User);
                }
                PltdAppRegistered = pltd.IsRegistered();
                if (PltdAppRegistered != true)
                {
                    LogMessage($"Failed to register {SelectedPltdInfo.Name}. Unknown reason.");
                }
            }
            else
            {
                LogMessage($"Failed to register {SelectedPltdInfo.Name}. PhoneLineTransportDevice Access Denied: {accessStatus}");
            }
        }

        [RelayCommand(CanExecute = nameof(CanConnectAsync))]
        private async Task ConnectAsync()
        {
            Trace.Assert(SelectedPltdInfo != null);
            PhoneLineTransportDevice pltd = PhoneLineTransportDevice.FromId(SelectedPltdInfo!.Id);
            DeviceAccessStatus accessStatus = await pltd.RequestAccessAsync();
            if (accessStatus == DeviceAccessStatus.Allowed)
            {
                PltdConnected = await pltd.ConnectAsync();
                if (PltdConnected != true)
                {
                    LogMessage($"Failed to connect {SelectedPltdInfo.Name}. Unknown reason.");
                }
            }
            else
            {
                LogMessage($"Unable to connect {SelectedPltdInfo.Name}. PhoneLineTransportDevice Access Denied: {accessStatus}.");
            }
        }

        private bool CanConnectAsync() { return SelectedPltdInfo != null; }

        private void _pltdWatcher_Added(DeviceWatcher sender, DeviceInformation args)
        {
            _dispatcherQueue.TryEnqueue(() =>
            {
                PltdInfos.Add(args);
            });
        }

        private void _pltdWatcher_Removed(DeviceWatcher sender, DeviceInformationUpdate args)
        {
            _dispatcherQueue.TryEnqueue(() =>
            {
                PltdInfos.Remove(PltdInfos.Where(df => df.Id == args.Id).First());
            });
        }

        public void LogMessage(string message)
        {
            string m = $"{DateTime.Now:HH:mm} # {message}";

#if DEBUG
            Debug.WriteLine(m);
#endif
            Logs.Add(m);
        }

        public void Dispose()
        {
            if (_pltdWatcher.Status == DeviceWatcherStatus.Started || 
                _pltdWatcher.Status == DeviceWatcherStatus.EnumerationCompleted)
            {
                _pltdWatcher.Stop();
            }
        }
    }
}
