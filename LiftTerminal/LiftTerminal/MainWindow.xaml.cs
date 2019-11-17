using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace LiftTerminal
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        TerminalViewModel _terminalViewModel;
        Dictionary<Button, Action> _buttonDownRegistry;// = new Dictionary<Button, Action<ButtonType, int>>


        Dictionary<Button, Action> _buttonUpRegistry;// = new Dictionary<Button, Action<ButtonType, int>>
        

        public MainWindow()
        {
            InitializeComponent();
            _terminalViewModel = new TerminalViewModel();
            this.DataContext = _terminalViewModel;
            InitButtonHandler();
            AddHandler(FrameworkElement.MouseLeftButtonDownEvent, new MouseButtonEventHandler(Button_MouseLeftDown), true);
            AddHandler(FrameworkElement.MouseLeftButtonUpEvent, new MouseButtonEventHandler(Button_MouseLeftUp), true);
            Dispatcher.UnhandledException += Dispatcher_UnhandledException;
        }

        private void Dispatcher_UnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            System.Windows.MessageBox.Show(e.Exception.Message, "Exception occurred", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;

        }

        void InitButtonHandler()
        {
            _buttonUpRegistry = new Dictionary<Button, Action>
            {
                { BtnC1, ()=>{_terminalViewModel.SendButtonUp(ButtonType.Cabine,0); } },
                { BtnC2, ()=>{_terminalViewModel.SendButtonUp(ButtonType.Cabine,1); } },
                { BtnC3, ()=>{_terminalViewModel.SendButtonUp(ButtonType.Cabine,2); } },
                { BtnC4, ()=>{_terminalViewModel.SendButtonUp(ButtonType.Cabine,3); } },

                { BtnF1, ()=>{_terminalViewModel.SendButtonUp(ButtonType.Floor,0); } },
                { BtnF2, ()=>{_terminalViewModel.SendButtonUp(ButtonType.Floor,1); } },
                { BtnF3, ()=>{_terminalViewModel.SendButtonUp(ButtonType.Floor,2); } },
                { BtnF4, ()=>{_terminalViewModel.SendButtonUp(ButtonType.Floor,3); } },
            };

            _buttonDownRegistry = new Dictionary<Button, Action>
            {
                { BtnC1, ()=>{_terminalViewModel.SendButtonDown(ButtonType.Cabine,0); } },
                { BtnC2, ()=>{_terminalViewModel.SendButtonDown(ButtonType.Cabine,1); } },
                { BtnC3, ()=>{_terminalViewModel.SendButtonDown(ButtonType.Cabine,2); } },
                { BtnC4, ()=>{_terminalViewModel.SendButtonDown(ButtonType.Cabine,3); } },

                { BtnF1, ()=>{_terminalViewModel.SendButtonDown(ButtonType.Floor,0); } },
                { BtnF2, ()=>{_terminalViewModel.SendButtonDown(ButtonType.Floor,1); } },
                { BtnF3, ()=>{_terminalViewModel.SendButtonDown(ButtonType.Floor,2); } },
                { BtnF4, ()=>{_terminalViewModel.SendButtonDown(ButtonType.Floor,3); } },
            };

        }
        private void Button_MouseLeftDown(object sender, MouseButtonEventArgs e)
        {
            var button = e.Source as Button;
            if (button != null)
            {
                if (_buttonDownRegistry.TryGetValue(button, out Action action))
                {
                    action();
                }
            }
        }

        private void Button_MouseLeftUp(object sender, MouseButtonEventArgs e)
        {
            var button = e.Source as Button;
            if (button != null)
            {
                if (_buttonUpRegistry.TryGetValue(button, out Action action))
                {
                    action();
                }
            }
        }
        private void BtnE1_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _terminalViewModel.SendButtonDown(ButtonType.Floor, 1);
        }

        private void BtnE1_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _terminalViewModel.SendButtonUp(ButtonType.Floor, 1);
        }
        private void BtnE2_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _terminalViewModel.SendButtonDown(ButtonType.Floor, 2);
        }

        private void BtnE2_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _terminalViewModel.SendButtonUp(ButtonType.Floor, 2);
        }

        private void BtnE3_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _terminalViewModel.SendButtonDown(ButtonType.Floor, 3);
        }

        private void BtnE3_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _terminalViewModel.SendButtonUp(ButtonType.Floor, 3);
        }

        private void BtnE4_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _terminalViewModel.SendButtonDown(ButtonType.Floor, 4);
        }

        private void BtnE4_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _terminalViewModel.SendButtonUp(ButtonType.Floor, 4);
        }

        private void BtnC1_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _terminalViewModel.SendButtonDown(ButtonType.Cabine, 1);
        }

        private void BtnC1_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _terminalViewModel.SendButtonUp(ButtonType.Cabine, 1);
        }

        private void BtnC2_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _terminalViewModel.SendButtonDown(ButtonType.Cabine, 2);
        }

        private void BtnC2_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _terminalViewModel.SendButtonUp(ButtonType.Cabine, 2);
        }

        private void BtnC3_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _terminalViewModel.SendButtonDown(ButtonType.Cabine, 3);
        }

        private void BtnC3_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _terminalViewModel.SendButtonUp(ButtonType.Cabine, 3);
        }

        private void BtnC4_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _terminalViewModel.SendButtonDown(ButtonType.Cabine, 4);
        }

        private void BtnC4_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _terminalViewModel.SendButtonUp(ButtonType.Cabine, 4);
        }
    }
}
