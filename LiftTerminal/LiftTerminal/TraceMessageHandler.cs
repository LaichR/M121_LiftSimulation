using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Text.Json.Serialization;
using PlotLib.DataSources;


namespace AvrTerminal
{
    public class TraceInfo
    {
        delegate int PackParamDelegate(byte[] data );
        
        List<PackParamDelegate> _unpackParams = new List<PackParamDelegate>();

        Func<object[], object> EvalData;

        TraceInfoType _type;

        string _reformattedTrace;
        public string @class { get; set; }
        public int MsgId { get; set; }
        public string TraceString { get; set; }

        public TraceInfoType InfoType => _type;
        

        public string File { get; set; }
        public int LineNumber { get; set; }
        public int NumberOfArguments { get; set; }

        public void UpdateTraceInfo()
        {
            _type = TraceInfoType.TraceMessage;
            var re = new Regex(@"%(?<type>[sdhf])?(?<size>\d\d?)");
            var matches = re.Matches(TraceString);
            
            _reformattedTrace = TraceString.Trim('"');
            var offset = 2; // the trace id must not be read anymore!
            for (int i = 0; i < matches.Count; i++)
            {
                var nrOfBytes = int.Parse(matches[i].Groups["size"].Value) / 8;
                var localOffset = offset;
                _unpackParams.Add( (data) => TraceMessageHandler.Unpack(data, localOffset, nrOfBytes) );
                offset += nrOfBytes;
                var replacement = string.Format("{{{0}:X02}}", i);
                _reformattedTrace = re.Replace(_reformattedTrace, replacement, 1);
            }
            EvalData = (x) => string.Format(_reformattedTrace, x);
            if (TraceString.Contains("@plot"))
            {
                _type = TraceInfoType.PlotData;
                EvalData = (x) => (double)Convert.ToDouble(x.First());
            }
        }

        public object EvalTrace(byte[] data )
        {
            List<object> unpacked = new List<object>();
            foreach( var up in _unpackParams)
            {
                unpacked.Add(up(data));
            }
            return EvalData(unpacked.ToArray());
        }

    }

    class TraceMessageHandler
    {
        Dictionary<int, TraceInfo> _traceDictionary = new Dictionary<int, TraceInfo>();
        FileSystemWatcher _traceInfoWatcher;
        TraceInfo _lastTraceInfo;
        string _fileName;

        public event EventHandler TraceInfoChanged;

        public TraceMessageHandler(string fileName)
        {
            _fileName = fileName;
            _traceInfoWatcher = new FileSystemWatcher(
                System.IO.Path.GetDirectoryName(fileName));
            _traceInfoWatcher.Changed += TraceInfo_Changed;
            _traceInfoWatcher.EnableRaisingEvents = true;
            InitializeTraceInfo(_fileName);
        }

        private void TraceInfo_Changed(object sender, FileSystemEventArgs e)
        {
            if( TraceInfoChanged != null)
            {
                TraceInfoChanged(this, EventArgs.Empty);
            }

            if( e.FullPath == _fileName)
            {
                InitializeTraceInfo(_fileName);
            }
        }

        public TraceInfoType EvalTrace( byte[] data, out object traceData)
        {
            var msgId = Unpack(data, 0, 2);
            var infoType = TraceInfoType.TraceMessage;
            traceData = string.Format("unknown message {0}", msgId);
            if (msgId == _lastTraceInfo?.MsgId)
            {
                infoType = _lastTraceInfo.InfoType;
                traceData = _lastTraceInfo.EvalTrace(data);
            }
            else
            {
                if (_traceDictionary.TryGetValue(msgId, out TraceInfo traceInfo))
                {
                    infoType = traceInfo.InfoType;
                    traceData = traceInfo.EvalTrace(data);
                    _lastTraceInfo = traceInfo;
                }
            }
            return infoType;
        }
        

        static public int Unpack(byte[] data, int offset, int nrOfBytes)
        {
            int val = data[offset];
            for (int i = 1; i < nrOfBytes; i++)
            {
                val <<= 8;
                val |= data[offset + i];
            }
            return val;
        }

        void InitializeTraceInfo(string fileName)
        {
            _traceDictionary.Clear();
            try
            {
                var jsonString = File.ReadAllText(fileName);


                var traceInfoList = JsonSerializer.Deserialize<TraceInfo[]>(jsonString);
                foreach (var t in traceInfoList)
                {
                    t.UpdateTraceInfo();
                    _traceDictionary.Add(t.MsgId, t);
                }
            }
            catch (System.IO.IOException){ }
        }
    }
}
