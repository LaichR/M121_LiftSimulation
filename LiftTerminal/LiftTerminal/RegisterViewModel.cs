using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Configuration;
using System.Collections.ObjectModel;
using Prism.Mvvm;
using Prism.Commands;
using Builder.MetadataDescription;
using System.IO;

namespace AvrTerminal
{
    class RegisterViewModel : BindableBase
    {
        static readonly string _rootDir = AppDomain.CurrentDomain.BaseDirectory;
        static readonly string atmega328 = "Atmega328p.xml";

        static readonly List<Bitfield> _emptyList = new List<Bitfield>();
        readonly ObservableCollection<HardwareSymbolAccess> _registerRoot = new ObservableCollection<HardwareSymbolAccess>();
        readonly ObservableCollection<SkalarField> _selectedRegs = new ObservableCollection<SkalarField>();
        readonly DelegateCommand _cmdWriteToDevice;
        readonly DelegateCommand _cmdReadFromDevice;
        readonly AvrRegAccess _regAccess;
        SkalarField _currentRegister;

        object _selectedValue;

        public RegisterViewModel(AvrRegAccess regAccess)
        {
            var fileName = System.IO.Path.Combine(_rootDir, atmega328);
            if ( !System.IO.File.Exists( fileName ))
            {
                fileName = System.IO.Path.Combine(
                    ConfigurationManager.AppSettings["HwDescription"], atmega328);
                if( !System.IO.File.Exists( fileName ))
                {
                    throw new FileNotFoundException("Unable to locate register description", atmega328);
                }
            }
            var regRoot = new HardwareSymbolAccess(fileName);
            _registerRoot.Add(regRoot);
            _regAccess = regAccess;
            _regAccess.DeviceConnection.PortStatusChanged += DeviceConnection_PortStatusChanged;
            regRoot.RegisterRegisterAccess(regAccess);
            _cmdWriteToDevice = new DelegateCommand(DoWrite);
            _cmdReadFromDevice = new DelegateCommand(DoRead);
            RaisePropertyChanged(nameof(CanRead));
            RaisePropertyChanged(nameof(CanWrite));
        }

        private void DeviceConnection_PortStatusChanged(object sender, bool e)
        {
            RaisePropertyChanged(nameof(CanRead));
            RaisePropertyChanged(nameof(CanWrite));
        }

        public bool CanRead
        {
            get
            {
                return _regAccess.CanAccessDevice &&
                    (_selectedValue != null) &&
                    ((_selectedValue is IMemorySymbol));
            }
        }

        public bool CanWrite
        {
            get
            {
                return CanRead;
            }
        }

        public ICommand CmdReadFromCurrentNode => _cmdReadFromDevice;
        public ICommand CmdWriteCurrentNode => _cmdWriteToDevice;

        public ObservableCollection<HardwareSymbolAccess> RegisterTree
        {
            get => _registerRoot;
        }

        public ObservableCollection<SkalarField> SelectedRegisters => _selectedRegs;
        
        public SkalarField CurrentRegister
        {
            get => _currentRegister;
            set
            {
                _selectedRegs.Clear();
                SetProperty<SkalarField>(ref _currentRegister, value);
                if (_currentRegister != null)
                {
                    _selectedRegs.Add(_currentRegister);
                }
                RaisePropertyChanged("Bitfields");
            }
        }

        public IList<Bitfield> Bitfields
        {
            get
            {
                var regs = _emptyList;
                if (_currentRegister != null)
                {
                    regs = new List<Bitfield>(_currentRegister.Children.OfType<Bitfield>());
                }
                return regs;
            }
        }



        internal void HandleValueChanged(object oldValue, object newValue)
        {
            if ( oldValue != newValue)
            {
                _selectedValue = newValue;
                if( _selectedValue is HardwareSymbolAccess hwAccess)
                {
                    CurrentRegister = null;
                }
                else if( _selectedValue is MemorySymbol memSymbol)
                {
                    CurrentRegister = FindFirstRegister(memSymbol);
                }
                else if( _selectedValue is StructField structField)
                {
                    CurrentRegister = FindFirstRegister(structField);
                }
                else if( _selectedValue is SkalarField skalar)
                {
                    CurrentRegister = skalar;
                }
            }
        }

        SkalarField FindFirstRegister(IMemorySymbol memSymbol)
        {
            var first = memSymbol.Children.First();
            if (first is SkalarField skalar)
                
            {
                return skalar;
                
            }
            else if (first is IMemorySymbol firstMemSymbol)
            {
                return FindFirstRegister(firstMemSymbol);
            }
            else
            {
                throw new NotSupportedException(
                    string.Format("unexped type {0}", first.GetType().Name));
            }
        }

        void DoRead()
        {
            var memSym = (IMemorySymbol)_selectedValue;
            memSym.Read();
            
            
        }

        void DoWrite()
        {
            var memSym = (IMemorySymbol)_selectedValue;
            memSym.Write();
        }

    }
}
