using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

namespace AutonomousRim.Core
{
    public sealed class TrialExchange
    {
        public int Schema=1;
        public List<PolicyTrial> Trials=new List<PolicyTrial>();
        public static void Export(string path,IEnumerable<PolicyTrial> trials)
        {
            var data=new TrialExchange();data.Trials.AddRange(trials);
            using(var writer=XmlWriter.Create(path,new XmlWriterSettings {Indent=true}))new XmlSerializer(typeof(TrialExchange)).Serialize(writer,data);
        }
        public static int Import(string path,EvidenceSettings archive)
        {
            if(new FileInfo(path).Length>2097152)throw new InvalidDataException("Arquivo de ensaios excede 2 MB.");
            TrialExchange data;
            using(var reader=XmlReader.Create(path,new XmlReaderSettings {DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=2097152}))
                data=(TrialExchange)new XmlSerializer(typeof(TrialExchange)).Deserialize(reader);
            if(data.Schema!=1 || data.Trials==null || data.Trials.Count>128)throw new InvalidDataException("Versão ou quantidade de ensaios inválida.");
            int added=0;foreach(var trial in data.Trials)if(archive.AddTrial(trial))added++;return added;
        }
    }
}
