using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Globalization;

namespace LapWest {
    // Plain-text decisions and exact source snapshots, bounded independently
    // of the older event/CSV logs. No additional Windows components required.
    public sealed class DiagnosticLog {
        public readonly string Folder,Session;
        int sequence;
        const long FrameBudget=20L*1024*1024;
        public DiagnosticLog(string folder) {
            Folder=folder;Session=DateTime.UtcNow.ToString("yyyyMMddTHHmmss",CultureInfo.InvariantCulture)+"-"+Guid.NewGuid().ToString("N").Substring(0,8);
            Directory.CreateDirectory(Folder);Directory.CreateDirectory(Path.Combine(Folder,"frames"));
        }
        static FileInfo[] Files(string folder,string pattern) {
            FileInfo[] files=new DirectoryInfo(folder).GetFiles(pattern);
            Array.Sort<FileInfo>(files,delegate(FileInfo a,FileInfo b){int n=a.LastWriteTimeUtc.CompareTo(b.LastWriteTimeUtc);return n!=0?n:String.CompareOrdinal(a.Name,b.Name);});
            return files;
        }
        public string SaveFrame(byte[] bytes) {
            if(bytes==null||bytes.Length==0)return "";
            string ext=bytes.Length>7&&bytes[0]==137&&bytes[1]==80&&bytes[2]==78&&bytes[3]==71?".png":
                bytes.Length>2&&bytes[0]==255&&bytes[1]==216?".jpg":bytes.Length>3&&bytes[0]==71&&bytes[1]==73&&bytes[2]==70?".gif":
                bytes.Length>2&&bytes[0]==66&&bytes[1]==77?".bmp":".image";
            string name=DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff",CultureInfo.InvariantCulture)+"-"+Session+"-"+(++sequence).ToString("D6",CultureInfo.InvariantCulture)+ext;
            string relative="frames/"+name,path=Path.Combine(Path.Combine(Folder,"frames"),name),temp=path+".tmp";
            try {File.WriteAllBytes(temp,bytes);File.Move(temp,path);}finally {if(File.Exists(temp))File.Delete(temp);}
            FileInfo[] files=Files(Path.Combine(Folder,"frames"),"*.*");long size=0;foreach(FileInfo f in files)size+=f.Length;
            int remaining=files.Length;
            foreach(FileInfo f in files) {if(remaining<=60&&size<=FrameBudget)break;long length=f.Length;f.Delete();remaining--;size-=length;}
            return relative;
        }
        public void Write(string origin,string body) {
            string path=Path.Combine(Folder,"decisions-"+DateTime.UtcNow.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture)+".log");
            if(File.Exists(path)&&new FileInfo(path).Length>4L*1024*1024)
                File.Move(path,Path.Combine(Folder,"decisions-"+DateTime.UtcNow.ToString("yyyy-MM-dd-HHmmssfff",CultureInfo.InvariantCulture)+"-"+Guid.NewGuid().ToString("N").Substring(0,6)+".log"));
            string header="\r\n=== "+DateTime.UtcNow.ToString("o",CultureInfo.InvariantCulture)+" | "+Session+" | "+(++sequence).ToString("D6",CultureInfo.InvariantCulture)+" | "+origin+" ===\r\n";
            File.AppendAllText(path,header+body.Replace("\r\n","\n").Replace("\n","\r\n")+"\r\n",Encoding.UTF8);
            FileInfo[] files=Files(Folder,"decisions-*.log");
            for(int i=0;i<files.Length-6;i++)files[i].Delete();
        }
        public void Export(string destination,string information,Dictionary<string,string> references) {
            string temp=destination+".tmp-"+Guid.NewGuid().ToString("N");
            try {
                using(StoredZip zip=new StoredZip(temp)) {
                    zip.Add("READ-ME.txt",Encoding.UTF8.GetBytes(information));
                    foreach(FileInfo f in Files(Folder,"decisions-*.log"))zip.Add("decisions/"+f.Name,File.ReadAllBytes(f.FullName));
                    foreach(FileInfo f in Files(Path.Combine(Folder,"frames"),"*.*"))if(!f.Name.EndsWith(".tmp",StringComparison.OrdinalIgnoreCase))zip.Add("frames/"+f.Name,File.ReadAllBytes(f.FullName));
                    foreach(KeyValuePair<string,string> item in references)if(File.Exists(item.Value))zip.Add("references/"+item.Key,File.ReadAllBytes(item.Value));
                }
                if(File.Exists(destination)) {
                    try {File.Replace(temp,destination,null);}
                    catch(PlatformNotSupportedException){File.Copy(temp,destination,true);File.Delete(temp);}
                }else File.Move(temp,destination);
            }finally {if(File.Exists(temp))File.Delete(temp);}
        }
    }
    // Standard ZIP32 with STORE entries, compatible with CLR 2.0/.NET 3.5.
    // Camera JPEGs are already compressed; this avoids a modern ZIP dependency.
    sealed class StoredZip : IDisposable {
        sealed class Entry {public byte[] Name;public uint Crc,Length,Offset;public ushort Time,Date;}
        readonly BinaryWriter writer;
        readonly List<Entry> entries=new List<Entry>();
        static readonly uint[] CrcTable=MakeCrcTable();
        static uint[] MakeCrcTable() {
            uint[] table=new uint[256];for(uint i=0;i<256;i++){uint c=i;for(int k=0;k<8;k++)c=(c&1)!=0?0xedb88320U^(c>>1):c>>1;table[i]=c;}return table;
        }
        static uint Crc(byte[] data) {uint crc=0xffffffffU;foreach(byte b in data)crc=CrcTable[(crc^b)&255]^(crc>>8);return crc^0xffffffffU;}
        public StoredZip(string path) {writer=new BinaryWriter(new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None));}
        public void Add(string name,byte[] data) {
            name=name.Replace('\\','/');if(name.StartsWith("/",StringComparison.Ordinal)||name.IndexOf("../",StringComparison.Ordinal)>=0||name.IndexOf(':')>=0)throw new IOException("Invalid archive entry.");
            Entry e=new Entry();e.Name=Encoding.UTF8.GetBytes(name);if(e.Name.Length>65535||entries.Count>=65535)throw new IOException("Archive entry limit exceeded.");
            if(writer.BaseStream.Position>UInt32.MaxValue)throw new IOException("Archive is too large.");
            e.Offset=(uint)writer.BaseStream.Position;e.Crc=Crc(data);e.Length=(uint)data.Length;
            DateTime now=DateTime.Now;int year=Math.Max(1980,Math.Min(2107,now.Year));
            e.Time=(ushort)((now.Hour<<11)|(now.Minute<<5)|(now.Second/2));e.Date=(ushort)(((year-1980)<<9)|(now.Month<<5)|now.Day);
            writer.Write(0x04034b50U);writer.Write((ushort)20);writer.Write((ushort)0x0800);writer.Write((ushort)0);
            writer.Write(e.Time);writer.Write(e.Date);writer.Write(e.Crc);writer.Write(e.Length);writer.Write(e.Length);writer.Write((ushort)e.Name.Length);writer.Write((ushort)0);writer.Write(e.Name);writer.Write(data);entries.Add(e);
        }
        public void Dispose() {
            try {
                uint start=(uint)writer.BaseStream.Position;
                foreach(Entry e in entries) {
                    writer.Write(0x02014b50U);writer.Write((ushort)20);writer.Write((ushort)20);writer.Write((ushort)0x0800);writer.Write((ushort)0);
                    writer.Write(e.Time);writer.Write(e.Date);writer.Write(e.Crc);writer.Write(e.Length);writer.Write(e.Length);writer.Write((ushort)e.Name.Length);
                    writer.Write((ushort)0);writer.Write((ushort)0);writer.Write((ushort)0);writer.Write((ushort)0);writer.Write(0U);writer.Write(e.Offset);writer.Write(e.Name);
                }
                uint size=(uint)writer.BaseStream.Position-start;
                writer.Write(0x06054b50U);writer.Write((ushort)0);writer.Write((ushort)0);writer.Write((ushort)entries.Count);writer.Write((ushort)entries.Count);writer.Write(size);writer.Write(start);writer.Write((ushort)0);
            }finally {writer.Close();}
        }
    }
}
