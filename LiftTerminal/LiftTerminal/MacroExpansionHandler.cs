using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ScannerParser.Scanner;
using Clang.Preprocessor;
using Prism.Mvvm;
using System.Windows.Input;
using Prism.Commands;

namespace AvrTerminal
{
    class MacroExpansionHandler: BindableBase
    {
        
        ObservableCollection<PpDefineProxy> _ppDefines = new ObservableCollection<PpDefineProxy>();
        string _expanded;
        string _ppUsage;
        DelegateCommand _cmdExpand;
        DelegateCommand _cmdReadFile;
        DelegateCommand _cmdClearPpDefinitions;
        DelegateCommand _cmdCopyToClipboard; 

        PpContext _ppContext = new PpContext();

        List<string> _expansionTrace = new List<string>();
 

        public MacroExpansionHandler():base()
        {
            _cmdExpand = new DelegateCommand(EpandPpUsage, () => !string.IsNullOrEmpty(PpUsage));
            _cmdReadFile = new DelegateCommand(ReadPpDefinitionFile);
            _cmdClearPpDefinitions = new DelegateCommand(ClearPpDefinitions);
            _cmdCopyToClipboard = new DelegateCommand(CopyDefinesToClipboard);
        }

        public ObservableCollection<PpDefineProxy> PpDefines
        {
            get => _ppDefines;
        }

        public string PpUsage
        {
            get => _ppUsage;
            set
            {
                SetProperty<string>(ref _ppUsage, value);
                _cmdExpand.RaiseCanExecuteChanged();
                Expanded = "";
            }
        }

        public string Expanded
        {
            get => _expanded;
            set => SetProperty<string>(ref _expanded, value);
        }

        public ICommand CmdExpand
        {
            get => _cmdExpand;
        }

        public ICommand CmdReadFile
        {
            get => _cmdReadFile;
        }

        public ICommand CmdClearPpDefinitions
        {
            get => _cmdClearPpDefinitions;
        }

        public ICommand CmdCopyDefinesToClipboard
        {
            get => _cmdCopyToClipboard;
        }

        public void EpandPpUsage()
        {
            Expanded = "";
            _expansionTrace.Clear();
            PpContext ppContext = _ppContext;
            foreach( var ppDef in _ppDefines)
            {
                ppContext.SetPredefinedSymbol(ppDef.Name, ppDef.Replacement);
            }
            ppContext.ResetMacros();
            foreach( var ppDefine in ppContext.GetPpDefines())
            {
                ppDefine.ReleaseEventHandlers();
                ppDefine.MacroExpanded += OnMacroExpanded;
                ppDefine.MacroExpansionBegin += OnMacroExpansionBegin;
            }
    
            StringBuilder sb = new StringBuilder();
            var scanner = new ClangScanner(ppContext, null);
            scanner.ScanString(PpUsage, 0);
            var expanded = PpUtilities.JoinTokens(scanner);

            _expansionTrace.Add("***");
            _expansionTrace.Add(string.Format("*** {0}", expanded));
            _expansionTrace.Add("***");

            Expanded = string.Join("\n", _expansionTrace.ToArray()) ;
        }

        public void ReadPpDefinitionFile()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.DefaultExt = ".h";
            if( dlg.ShowDialog() == true )
            {
                var scanner = new ClangScanner(_ppContext, dlg.FileName);
                PpUtilities.JoinTokens(scanner);
            }
            foreach( var define  in _ppContext.GetPpDefines())
            {
                var macroArgs = ""; 
                if( define.Parameters != null && define.Parameters.Count() > 0 )
                {
                    macroArgs = string.Join(", ", define.Parameters.ToArray());
                    macroArgs = string.Format("({0})", macroArgs);
                }
                var proxName = string.Format("{0}{1}", define.Name, macroArgs);
                _ppDefines.Add(new PpDefineProxy() { Name = proxName, Replacement = define.Replacement });
            }
        }

        private void ClearPpDefinitions()
        {
            _ppDefines.Clear();
            _ppContext = new PpContext();
            _expanded = "";
            _ppUsage = "";
            RaisePropertyChanged("Expanded");
        }

        private void CopyDefinesToClipboard()
        {
            List<string> defineStrings = new List<string>(); 
            foreach( var ppDefine in _ppDefines)
            {
                var replacementString = ppDefine.Replacement;
                if( !string.IsNullOrEmpty(replacementString))
                {
                    var replacementLines = replacementString.Split('\n');
                    replacementString = string.Join("\\\n",
                        replacementLines.Select<string, string>((x) => x.Trim('\r')).ToArray());
                }
                defineStrings.Add(string.Format("\n#define {0}    {1}", ppDefine.Name, replacementString));
            }
            var ppDefineString = string.Join("\n", defineStrings.ToArray());
            System.Windows.Clipboard.SetText(ppDefineString);
        }

        private void OnMacroExpanded(object sender, MacroExpansionInfo e)
        {
            _expansionTrace.Add(
                string.Format("{0}End expansion of: {1}{2} => {3}",
                    Identation(e.RecursionDepth), e.Name, GetArgumentString(e), e.Expanded));
        }

        private void OnMacroExpansionBegin(object sender, MacroExpansionInfo e)
        {
            
            _expansionTrace.Add(
                string.Format("{0}Begin expansion of:  {1} {2}",
                    Identation(e.RecursionDepth), e.Name, GetArgumentString(e)));

        }

        string Identation(int recursion)
        {
            StringBuilder sb = new StringBuilder();
            for(int i = 0; i< recursion; i++)
            {
                sb.Append("    ");
            }
            return sb.ToString();
        }

        string GetArgumentString(MacroExpansionInfo e)
        {
            if(string.IsNullOrEmpty(e.Arguments))
            {
                return "";
            }
            return string.Format("({0})", e.Arguments);
        }
    }

    class PpDefineProxy
    {
        public string Name
        {
            get;
            set;
        }

        public string Replacement
        {
            get;
            set;
        }
    }
}
