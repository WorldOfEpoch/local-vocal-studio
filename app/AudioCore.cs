// Local Vocal Studio: local shared-mode WASAPI, no default-device changes or network.
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Collections.Generic;
namespace AlexListen {
[ComImport,Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")] class MMEnumerator {}
[ComImport,Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IEnum {
 [PreserveSig]int EnumAudioEndpoints(int f,uint s,out IntPtr d);[PreserveSig]int GetDefaultAudioEndpoint(int f,int r,out IDevice d);[PreserveSig]int GetDevice([MarshalAs(UnmanagedType.LPWStr)]string id,out IDevice d);[PreserveSig]int RegisterEndpointNotificationCallback(IntPtr p);[PreserveSig]int UnregisterEndpointNotificationCallback(IntPtr p);
}
[ComImport,Guid("D666063F-1587-4E43-81F1-B948E807363F"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IDevice {
 [PreserveSig]int Activate(ref Guid id,uint c,IntPtr p,[MarshalAs(UnmanagedType.IUnknown)]out object v);[PreserveSig]int OpenPropertyStore(uint a,out IntPtr p);[PreserveSig]int GetId([MarshalAs(UnmanagedType.LPWStr)]out string id);[PreserveSig]int GetState(out uint s);
}
[ComImport,Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface ICollection {[PreserveSig]int GetCount(out uint n);[PreserveSig]int Item(uint n,out IDevice d);}
[StructLayout(LayoutKind.Sequential)]struct Key {public Guid fmtid;public uint pid;}
[StructLayout(LayoutKind.Explicit,Size=24)]struct Variant {[FieldOffset(0)]public ushort vt;[FieldOffset(8)]public IntPtr pointer;}
[ComImport,Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface IProps {[PreserveSig]int GetCount(out uint n);[PreserveSig]int GetAt(uint n,out Key k);[PreserveSig]int GetValue(ref Key k,out Variant v);}
public sealed class DeviceInfo {public string Id,Name;public override string ToString(){return Name;}}
public static class Devices {
 [DllImport("ole32.dll")]static extern int PropVariantClear(ref Variant v);
 public static DeviceInfo[] List(bool input){var list=new List<DeviceInfo>();var en=(IEnum)new MMEnumerator();IntPtr ptr;int hr=en.EnumAudioEndpoints(input?1:0,1,out ptr);if(hr<0)Marshal.ThrowExceptionForHR(hr);var collection=(ICollection)Marshal.GetObjectForIUnknown(ptr);Marshal.Release(ptr);uint count;collection.GetCount(out count);for(uint i=0;i<count;i++){IDevice d;collection.Item(i,out d);string id;d.GetId(out id);IntPtr p;d.OpenPropertyStore(0,out p);var props=(IProps)Marshal.GetObjectForIUnknown(p);Marshal.Release(p);var key=new Key{fmtid=new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"),pid=14};Variant v;props.GetValue(ref key,out v);string name=v.vt==31?Marshal.PtrToStringUni(v.pointer):"Audio endpoint";PropVariantClear(ref v);list.Add(new DeviceInfo{Id=id,Name=name});Marshal.ReleaseComObject(props);Marshal.ReleaseComObject(d);}Marshal.ReleaseComObject(collection);Marshal.ReleaseComObject(en);return list.ToArray();}
}
public static class Routing {
 public static float ListenGain(bool enabled,float db){return enabled?(float)Math.Pow(10,Math.Max(-40,Math.Min(0,db))/20):0;}
 public static float MainSongOnly(float song,float smoothedGain){return Math.Max(-.501f,Math.Min(.501f,song*smoothedGain));}
 public static float PrivateVoiceAndMusic(float voice,float music){return Math.Max(-.501f,Math.Min(.501f,voice+music));}
}
[ComImport,Guid("7ED4EE07-8E67-4CD4-8C1A-2B7A5987AD42"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IClient {
 [PreserveSig]int Initialize(uint mode,uint flags,long duration,long period,IntPtr format,ref Guid session);[PreserveSig]int GetBufferSize(out uint frames);[PreserveSig]int GetStreamLatency(out long latency);[PreserveSig]int GetCurrentPadding(out uint padding);[PreserveSig]int IsFormatSupported(uint mode,IntPtr format,out IntPtr closest);[PreserveSig]int GetMixFormat(out IntPtr format);[PreserveSig]int GetDevicePeriod(out long normal,out long min);[PreserveSig]int Start();[PreserveSig]int Stop();[PreserveSig]int Reset();[PreserveSig]int SetEventHandle(IntPtr h);[PreserveSig]int GetService(ref Guid id,[MarshalAs(UnmanagedType.IUnknown)]out object service);
 [PreserveSig]int IsOffloadCapable(int category,[MarshalAs(UnmanagedType.Bool)]out bool capable);[PreserveSig]int SetClientProperties(IntPtr properties);[PreserveSig]int GetBufferSizeLimits(IntPtr format,[MarshalAs(UnmanagedType.Bool)]bool eventDriven,out long min,out long max);
 [PreserveSig]int GetSharedModeEnginePeriod(IntPtr format,out uint normal,out uint fundamental,out uint min,out uint max);[PreserveSig]int GetCurrentSharedModeEnginePeriod(out IntPtr format,out uint frames);[PreserveSig]int InitializeSharedAudioStream(uint flags,uint frames,IntPtr format,ref Guid session);
}
[ComImport,Guid("C8ADBD64-E71E-48A0-A4DE-185C395CD317"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface ICapture {
 [PreserveSig]int GetBuffer(out IntPtr data,out uint frames,out uint flags,out ulong devicePosition,out ulong qpcPosition);[PreserveSig]int ReleaseBuffer(uint frames);[PreserveSig]int GetNextPacketSize(out uint frames);
}
[ComImport,Guid("F294ACFC-3146-4483-A7BF-ADDCA7C260E2"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IRender {
 [PreserveSig]int GetBuffer(uint frames,out IntPtr data);[PreserveSig]int ReleaseBuffer(uint frames,uint flags);
}
sealed class Port : IDisposable {
 public IClient Client; public object Service; public uint Period,Buffer;public double StreamLatencyMs;
 public AutoResetEvent Event=new AutoResetEvent(false);IDevice device;
 static void OK(int hr){if(hr<0)Marshal.ThrowExceptionForHR(hr);}
 public Port(string id,bool input,bool loopback=false){try{
  var en=(IEnum)new MMEnumerator();OK(en.GetDevice(id,out device));Marshal.ReleaseComObject(en);
  object o;Guid iid=typeof(IClient).GUID;OK(device.Activate(ref iid,23,IntPtr.Zero,out o));Client=(IClient)o;
  IntPtr fmt;OK(Client.GetMixFormat(out fmt));
  try {
   int tag=(ushort)Marshal.ReadInt16(fmt,0),channels=(ushort)Marshal.ReadInt16(fmt,2),rate=Marshal.ReadInt32(fmt,4),bits=(ushort)Marshal.ReadInt16(fmt,14);
   bool floats=tag==3||(tag==65534&&Marshal.ReadInt32(fmt,24)==3);
   if(rate!=48000||channels!=2||bits!=32||!floats)throw new Exception("Each selected endpoint must currently use a 48 kHz stereo float shared mix format. No Windows device settings were changed.");
   uint normal,fund,min,max;OK(Client.GetSharedModeEnginePeriod(fmt,out normal,out fund,out min,out max));Period=normal;
   uint flags=0x40000u|(loopback?0x20000u:0u);Guid session=new Guid("E8AB8D26-6C56-4875-BB3B-B205C962127C");
   int hr=loopback?Client.Initialize(0,flags,1000000,0,fmt,ref session):Client.InitializeSharedAudioStream(flags,Period,fmt,ref session);
   if(hr<0&&Period!=normal){Period=normal;hr=Client.InitializeSharedAudioStream(flags,Period,fmt,ref session);}OK(hr);
   OK(Client.SetEventHandle(Event.SafeWaitHandle.DangerousGetHandle()));OK(Client.GetBufferSize(out Buffer));long latency;OK(Client.GetStreamLatency(out latency));StreamLatencyMs=latency/10000.0;
   iid=input?typeof(ICapture).GUID:typeof(IRender).GUID;OK(Client.GetService(ref iid,out Service));
  } finally {Marshal.FreeCoTaskMem(fmt);}
 }catch{Dispose();throw;}}
 public void Dispose(){if(Client!=null){Client.Stop();if(Service!=null)Marshal.ReleaseComObject(Service);Marshal.ReleaseComObject(Client);Client=null;}if(device!=null){Marshal.ReleaseComObject(device);device=null;}Event.Dispose();}
}
sealed class Wave24 : IDisposable {
 BinaryWriter w;long bytes;byte[] buffer=new byte[2048*6];
 public Wave24(string path){w=new BinaryWriter(new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.Read));w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));w.Write(0);w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));w.Write(16);w.Write((ushort)1);w.Write((ushort)2);w.Write(48000);w.Write(48000*6);w.Write((ushort)6);w.Write((ushort)24);w.Write(System.Text.Encoding.ASCII.GetBytes("data"));w.Write(0);}
 public void Write(float[] data,int frames){if(bytes+frames*6>4294967200L)throw new Exception("Recording reached the WAV size limit. Start another take.");for(int i=0;i<frames*2;i++){int s=(int)(Math.Max(-1,Math.Min(.99999988,data[i]))*8388607);buffer[i*3]=(byte)s;buffer[i*3+1]=(byte)(s>>8);buffer[i*3+2]=(byte)(s>>16);}w.Write(buffer,0,frames*6);bytes+=frames*6;}
 public void Dispose(){if(w==null)return;w.Seek(4,SeekOrigin.Begin);w.Write((int)(bytes+36));w.Seek(40,SeekOrigin.Begin);w.Write((int)bytes);w.Dispose();w=null;}
}
sealed class Song : IDisposable {
 BinaryReader reader;long start,end;int bits,tag;public double Duration,Position;
 public Song(string path){reader=new BinaryReader(File.Open(path,FileMode.Open,FileAccess.Read,FileShare.Read));try{if(new string(reader.ReadChars(4))!="RIFF")throw new Exception("Use a WAV file, or install optional FFmpeg for MP3 and other formats.");reader.ReadUInt32();if(new string(reader.ReadChars(4))!="WAVE")throw new Exception("Invalid WAV file.");int channels=0,rate=0;while(reader.BaseStream.Position+8<=reader.BaseStream.Length){string chunk=new string(reader.ReadChars(4));uint size=reader.ReadUInt32();long next=reader.BaseStream.Position+size+(size&1);if(chunk=="fmt "){tag=reader.ReadUInt16();channels=reader.ReadUInt16();rate=reader.ReadInt32();reader.ReadInt32();reader.ReadUInt16();bits=reader.ReadUInt16();if(tag==65534&&size>=40){reader.ReadUInt16();reader.ReadUInt16();reader.ReadUInt32();tag=reader.ReadUInt16();}}else if(chunk=="data"){start=reader.BaseStream.Position;end=Math.Min(start+size,reader.BaseStream.Length);}reader.BaseStream.Position=next;}if(start==0||channels!=2||rate!=48000||!((tag==1&&(bits==16||bits==24||bits==32))||(tag==3&&bits==32)))throw new Exception("Song must be 48 kHz stereo PCM/float WAV. Optional FFmpeg converts other formats locally.");Duration=(end-start)/(48000.0*2*(bits/8));reader.BaseStream.Position=start;}catch{reader.Dispose();throw;}}
 public float Read(){if(reader.BaseStream.Position+bits/8>end)return 0;float v;if(tag==3)v=reader.ReadSingle();else if(bits==16)v=reader.ReadInt16()/32768f;else if(bits==24){int n=reader.ReadByte()|(reader.ReadByte()<<8)|(reader.ReadByte()<<16);if((n&0x800000)!=0)n|=unchecked((int)0xff000000);v=n/8388608f;}else v=reader.ReadInt32()/2147483648f;Position=(reader.BaseStream.Position-start)/(48000.0*2*(bits/8));return float.IsNaN(v)||float.IsInfinity(v)?0:v;}
 public void Rewind(){reader.BaseStream.Position=start;Position=0;}
 public void Seek(double seconds){long frame=(long)(Math.Max(0,Math.Min(Duration,seconds))*48000);reader.BaseStream.Position=Math.Min(end,start+frame*2*(bits/8));Position=(reader.BaseStream.Position-start)/(48000.0*2*(bits/8));}
 public void Dispose(){reader.Dispose();}
}
sealed class Pitch : IDisposable {
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]static extern IntPtr LoadLibrary(string p);[DllImport("kernel32.dll",CharSet=CharSet.Ansi)]static extern IntPtr GetProcAddress(IntPtr m,string n);[DllImport("kernel32.dll")]static extern bool FreeLibrary(IntPtr m);
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)]delegate IntPtr Host(IntPtr e,int op,int i,IntPtr v,IntPtr p,float f);
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)]delegate IntPtr Entry(Host h);
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)]delegate void Param(IntPtr e,int i,float v);
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)]delegate void Process(IntPtr e,IntPtr ins,IntPtr outs,int frames);
 [StructLayout(LayoutKind.Sequential)]struct Effect {public int magic;public IntPtr dispatcher,process,set,get;public int programs,parameters,inputs,outputs,flags;public IntPtr reserved1,reserved2;public int delay,real,offline;public float ratio;public IntPtr obj,user;public int uid,version;public IntPtr replacing,doubles;[MarshalAs(UnmanagedType.ByValArray,SizeConst=56)]public byte[] future;}
 IntPtr module,effect,ins,outs;Host host,dispatch;Process process;Param set;GCHandle lpin,rpin,olpin,orpin;float[] l=new float[2048],r=new float[2048],ol=new float[2048],orr=new float[2048];
 public int DelaySamples;
 public Pitch(string path){
  host=(e,op,i,v,p,f)=>{if(op==1)return new IntPtr(2400);if(op==16)return new IntPtr(48000);if(op==17)return new IntPtr(2048);return IntPtr.Zero;};
  module=LoadLibrary(path);if(module==IntPtr.Zero)throw new Exception("Approved GSnap plugin could not load.");IntPtr entry=GetProcAddress(module,"VSTPluginMain");if(entry==IntPtr.Zero)throw new Exception("GSnap VST entry missing.");effect=Marshal.GetDelegateForFunctionPointer<Entry>(entry)(host);Effect fx=Marshal.PtrToStructure<Effect>(effect);if(fx.magic!=0x56737450||fx.inputs!=2||fx.outputs!=2)throw new Exception("Unexpected GSnap format.");
  dispatch=Marshal.GetDelegateForFunctionPointer<Host>(fx.dispatcher);process=Marshal.GetDelegateForFunctionPointer<Process>(fx.replacing);set=Marshal.GetDelegateForFunctionPointer<Param>(fx.set);DelaySamples=fx.delay;
  dispatch(effect,0,0,IntPtr.Zero,IntPtr.Zero,0);dispatch(effect,10,0,IntPtr.Zero,IntPtr.Zero,48000);dispatch(effect,11,0,new IntPtr(2048),IntPtr.Zero,0);set(effect,5,.85f);set(effect,6,.04f);set(effect,7,.10f);dispatch(effect,12,0,new IntPtr(1),IntPtr.Zero,0);
  lpin=GCHandle.Alloc(l,GCHandleType.Pinned);rpin=GCHandle.Alloc(r,GCHandleType.Pinned);olpin=GCHandle.Alloc(ol,GCHandleType.Pinned);orpin=GCHandle.Alloc(orr,GCHandleType.Pinned);ins=Marshal.AllocHGlobal(IntPtr.Size*2);outs=Marshal.AllocHGlobal(IntPtr.Size*2);Marshal.WriteIntPtr(ins,0,lpin.AddrOfPinnedObject());Marshal.WriteIntPtr(ins,IntPtr.Size,rpin.AddrOfPinnedObject());Marshal.WriteIntPtr(outs,0,olpin.AddrOfPinnedObject());Marshal.WriteIntPtr(outs,IntPtr.Size,orpin.AddrOfPinnedObject());
 }
 public void Apply(float[] data,int frames){for(int i=0;i<frames;i++){l[i]=data[i*2];r[i]=data[i*2+1];}process(effect,ins,outs,frames);for(int i=0;i<frames;i++){data[i*2]=ol[i];data[i*2+1]=orr[i];}}
 public void Configure(int mask,float amount,float attackMs,float releaseMs){set(effect,4,.15966387f);set(effect,5,Math.Max(0,Math.Min(1,amount/100)));set(effect,6,Math.Max(0,Math.Min(1,(attackMs-1)/299)));set(effect,7,Math.Max(0,Math.Min(1,(releaseMs-1)/299)));for(int i=0;i<12;i++)set(effect,8+i,(mask&(1<<i))!=0?1:0);set(effect,20,0);}
 public void Dispose(){if(effect!=IntPtr.Zero){dispatch(effect,12,0,IntPtr.Zero,IntPtr.Zero,0);dispatch(effect,1,0,IntPtr.Zero,IntPtr.Zero,0);effect=IntPtr.Zero;}if(ins!=IntPtr.Zero){lpin.Free();rpin.Free();olpin.Free();orpin.Free();Marshal.FreeHGlobal(ins);Marshal.FreeHGlobal(outs);ins=IntPtr.Zero;}if(module!=IntPtr.Zero){FreeLibrary(module);module=IntPtr.Zero;}}
}
public sealed class Engine : IDisposable {
 [DllImport("avrt.dll",CharSet=CharSet.Unicode)]static extern IntPtr AvSetMmThreadCharacteristics(string task,out uint index);[DllImport("avrt.dll")]static extern bool AvRevertMmThreadCharacteristics(IntPtr handle);
 public volatile bool Hear,Dry,UsePitch,TestTone,PlaySong,LoopSong,MainMusic;public volatile int Preset;public volatile float VolumeDb=-24,MainVolumeDb=-24,MicGainDb=0,SongDb=-18,Compression=60,Reverb=0,Delay=0;
 public volatile int PitchMask=4095;public volatile float PitchAmount=65,PitchAttack=125,PitchRelease=150;
 public volatile float MicPeak,OutputPeak,SongPeak,PitchHz;public volatile bool Running;public string Error="",RecordPath="",RouteInfo="Starting selected endpoints...";public uint CapturePeriod,RenderPeriod,CaptureBuffer,RenderBuffer;public double CaptureStreamMs,RenderStreamMs,QueueAgeMs;public long RenderBlocks,StarvedBlocks,DiscontinuousPackets;
 Thread thread;ManualResetEvent stop=new ManualResetEvent(false);string root,inputId,outputId,pitchPath,mainId;bool mirrorSystem,disposed;object recordingLock=new object(),songLock=new object();Wave24 dryFile,wetFile;Pitch pitch;Song song;public double SongPosition,SongDuration;public volatile float MainPeak,MirrorPeak;
 float[] delayBuffer=new float[48000*2],reverbBuffer=new float[48000*2],pitchWindow=new float[4096];int effectPos,pitchPos,pitchCounter;float[] eqLow=new float[2];
 float[] low=new float[2],smooth=new float[2],hp=new float[2],last=new float[2];double envelope;float monitorGain;
 public bool IsRecording {get{lock(recordingLock)return dryFile!=null;}}
 public sealed class OfflineResult {public float[] Samples;public float PitchHz;}
 Engine(){}
 public static OfflineResult RenderOffline(float[] raw,int preset,float compression,float reverb,float delay,string optionalPitch){using(var e=new Engine()){e.Preset=preset;e.Compression=compression;e.Reverb=reverb;e.Delay=delay;e.pitchPath=optionalPitch;e.UsePitch=!String.IsNullOrEmpty(optionalPitch);float[] all=new float[raw.Length],src=new float[960],dst=new float[960];for(int offset=0;offset<raw.Length;offset+=960){int n=Math.Min(960,raw.Length-offset);Array.Copy(raw,offset,src,0,n);e.Effects(src,dst,n/2);Array.Copy(dst,0,all,offset,n);}return new OfflineResult{Samples=all,PitchHz=e.PitchHz};}}
 static void OK(int hr){if(hr<0)Marshal.ThrowExceptionForHR(hr);}
 public Engine(string studioRoot,string inputDevice,string outputDevice,string optionalPitchPlugin):this(studioRoot,inputDevice,outputDevice,optionalPitchPlugin,null,false){}
 public Engine(string studioRoot,string inputDevice,string outputDevice,string optionalPitchPlugin,string mainOutput,bool mirrorMain){root=studioRoot;inputId=inputDevice;outputId=outputDevice;pitchPath=optionalPitchPlugin;mainId=mainOutput;mirrorSystem=mirrorMain;if(!String.IsNullOrEmpty(mainId)&&mainId==outputId)throw new Exception("Main and Headphones must be different endpoints to prevent doubled audio / a loop.");if(mirrorMain&&String.IsNullOrEmpty(mainId))throw new Exception("Select a Main Output to mirror.");thread=new Thread(Run);thread.IsBackground=true;thread.Priority=ThreadPriority.AboveNormal;thread.SetApartmentState(ApartmentState.MTA);thread.Start();}
 public void LoadSong(string path){var next=new Song(path);lock(songLock){if(song!=null)song.Dispose();song=next;PlaySong=false;SongPosition=0;SongDuration=song.Duration;}}
 public void RewindSong(){lock(songLock){if(song!=null)song.Rewind();SongPosition=0;}}
 public void SeekSong(double seconds){lock(songLock){if(song!=null){song.Seek(seconds);SongPosition=song.Position;}}}
 public void StartRecording(){lock(recordingLock){if(dryFile!=null)return;string dir=Path.Combine(root,"recordings");Directory.CreateDirectory(dir);RecordPath=Path.Combine(dir,"Practice-"+DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")+"-"+Guid.NewGuid().ToString("N").Substring(0,4));try{dryFile=new Wave24(RecordPath+"-DRY.wav");wetFile=new Wave24(RecordPath+"-FX.wav");}catch{if(dryFile!=null)dryFile.Dispose();dryFile=null;throw;}}}
 public void StopRecording(){lock(recordingLock){if(dryFile!=null){dryFile.Dispose();wetFile.Dispose();dryFile=null;wetFile=null;}}}
 void Effects(float[] raw,float[] wet,int frames){
  int preset=Preset;bool dry=false;double loGain=preset==1?1.58:preset==2?.79:1,hiGain=preset==1?.71:preset==2?1.58:1.12;double aLo=1-Math.Exp(-2*Math.PI*180/48000),aHi=1-Math.Exp(-2*Math.PI*3500/48000),aHp=Math.Exp(-2*Math.PI*65/48000);float micGain=(float)Math.Pow(10,Math.Max(-12,Math.Min(24,MicGainDb))/20);
  for(int i=0;i<frames;i++){double peak=Math.Max(Math.Abs(raw[i*2]),Math.Abs(raw[i*2+1]))*micGain;double envRate=peak>envelope?.0042:.00023;envelope+=(peak-envelope)*envRate;double db=20*Math.Log10(Math.Max(envelope,1e-9)),reduction=db>-18?(db+18)*Math.Max(0,Math.Min(.85,Compression/100.0)):0,gain=Math.Pow(10,-reduction/20);
   for(int c=0;c<2;c++){int j=i*2+c;float x=raw[j]*micGain;hp[c]=(float)(aHp*(hp[c]+x-last[c]));last[c]=x;low[c]+=(float)(aLo*(hp[c]-low[c]));smooth[c]+=(float)(aHi*(hp[c]-smooth[c]));double toned=low[c]*loGain+(smooth[c]-low[c])+(hp[c]-smooth[c])*hiGain;wet[j]=dry?x:(float)Math.Max(-.501,Math.Min(.501,toned*gain));}
  }
  if(UsePitch&&!dry){if(pitch==null)pitch=new Pitch(pitchPath);pitch.Configure(PitchMask,PitchAmount,PitchAttack,PitchRelease);pitch.Apply(wet,frames);}
  for(int i=0;i<frames;i++){for(int c=0;c<2;c++){int j=i*2+c;float x=wet[j];int di=((effectPos+48000-14400)%48000)*2+c,ri=((effectPos+48000-(c==0?7919:11003))%48000)*2+c;float echo=delayBuffer[di],room=reverbBuffer[ri];delayBuffer[effectPos*2+c]=x+echo*.25f;reverbBuffer[effectPos*2+c]=x+room*.55f;wet[j]=dry?x:Math.Max(-.501f,Math.Min(.501f,x+echo*Math.Max(0,Math.Min(.4f,Delay/100))+room*Math.Max(0,Math.Min(.4f,Reverb/100))));}effectPos=(effectPos+1)%48000;
   pitchWindow[pitchPos++]=(raw[i*2]+raw[i*2+1])*.5f;if(pitchPos==4096)pitchPos=0;if(++pitchCounter>=4800){pitchCounter=0;UpdatePitch();}
  }
 }
 void UpdatePitch(){double energy=0;for(int i=0;i<4096;i++)energy+=pitchWindow[i]*pitchWindow[i];if(energy/4096<1e-7){PitchHz=0;return;}double[] scores=new double[802];for(int lag=47;lag<=801;lag++){double xy=0,xx=0,yy=0;for(int i=0;i<2048;i+=2){float x=pitchWindow[(pitchPos+i)%4096],y=pitchWindow[(pitchPos+i+lag)%4096];xy+=x*y;xx+=x*x;yy+=y*y;}scores[lag]=xy/Math.Sqrt(Math.Max(1e-20,xx*yy));}PitchHz=0;for(int lag=48;lag<=800;lag++)if(scores[lag]>.90&&scores[lag]>scores[lag-1]&&scores[lag]>=scores[lag+1]){double delta=.5*(scores[lag-1]-scores[lag+1])/(scores[lag-1]-2*scores[lag]+scores[lag+1]);PitchHz=(float)(48000/(lag+delta));break;}}
 void Run(){Port input=null,output=null,main=null,mirror=null;uint taskIndex;IntPtr scheduling=AvSetMmThreadCharacteristics("Audio",out taskIndex);try{
  input=new Port(inputId,true);output=new Port(outputId,false);CapturePeriod=input.Period;RenderPeriod=output.Period;CaptureBuffer=input.Buffer;RenderBuffer=output.Buffer;CaptureStreamMs=input.StreamLatencyMs;RenderStreamMs=output.StreamLatencyMs;
  RouteInfo="Selected mic -> selected headphones | shared Windows audio | 48 kHz";
  if(!String.IsNullOrEmpty(mainId))main=new Port(mainId,false);if(mirrorSystem)mirror=new Port(mainId,true,true);
  var cap=(ICapture)input.Service;var render=(IRender)output.Service;int capFrames=65536,read=0,write=0,count=0;float[] ring=new float[capFrames*2];ulong[] stamps=new ulong[capFrames];float[] packet=new float[65536*2],raw=new float[4096],wet=new float[4096],sent=new float[4096];
  IntPtr initial;uint prime=output.Buffer;OK(render.GetBuffer(prime,out initial));OK(render.ReleaseBuffer(prime,2));OK(input.Client.Start());OK(output.Client.Start());Running=true;
  var waitList=new List<WaitHandle>{stop,input.Event,output.Event};if(main!=null){var rr=(IRender)main.Service;IntPtr p;OK(rr.GetBuffer(main.Buffer,out p));OK(rr.ReleaseBuffer(main.Buffer,2));OK(main.Client.Start());waitList.Add(main.Event);}if(mirror!=null){OK(mirror.Client.Start());waitList.Add(mirror.Event);}var waits=waitList.ToArray();double qpcFrequency=System.Diagnostics.Stopwatch.Frequency;long origin=System.Diagnostics.Stopwatch.GetTimestamp(),generatedFrames=0;bool wasEmpty=false;
  int auxFrames=16384,mr=0,mw=0,mc=0,lr=0,lw=0,lc=0;float[] mainRing=new float[auxFrames*2],mirrorRing=new float[auxFrames*2],songBlock=new float[4096];float mainGain=0;
  while(!stop.WaitOne(0)){
   WaitHandle.WaitAny(waits,20);
   if(main!=null){uint mp;OK(main.Client.GetCurrentPadding(out mp));int frames=main.Buffer-mp>=main.Period?(int)main.Period:0;if(frames>0){float peak=0,targetMain=MainMusic?(float)Math.Pow(10,Math.Max(-40,Math.Min(0,MainVolumeDb))/20):0;for(int i=0;i<frames;i++){mainGain+=(targetMain-mainGain)*.0021f;for(int c=0;c<2;c++){// This bus reads ONLY song samples. Microphone samples never enter mainRing.
       float v=mc>0?Routing.MainSongOnly(mainRing[mr*2+c],mainGain):0;packet[i*2+c]=v;peak=Math.Max(peak,Math.Abs(v));}if(mc>0){mr=(mr+1)%auxFrames;mc--;}}IntPtr md;var rr=(IRender)main.Service;OK(rr.GetBuffer((uint)frames,out md));Marshal.Copy(packet,0,md,frames*2);OK(rr.ReleaseBuffer((uint)frames,0));MainPeak=peak;}}
   if(mirror!=null){var cc=(ICapture)mirror.Service;uint nn;OK(cc.GetNextPacketSize(out nn));while(nn>0){IntPtr ptr;uint frames,flags;ulong pos,qpc;OK(cc.GetBuffer(out ptr,out frames,out flags,out pos,out qpc));bool silent=(flags&2)!=0;if(!silent)Marshal.Copy(ptr,packet,0,(int)frames*2);float peak=0;for(int i=0;i<frames;i++){if(lc==auxFrames){lr=(lr+1)%auxFrames;lc--;}for(int c=0;c<2;c++){float v=silent?0:packet[i*2+c];mirrorRing[lw*2+c]=v;peak=Math.Max(peak,Math.Abs(v));}lw=(lw+1)%auxFrames;lc++;}MirrorPeak=peak;OK(cc.ReleaseBuffer(frames));OK(cc.GetNextPacketSize(out nn));}}
   uint n;OK(cap.GetNextPacketSize(out n));while(n>0){IntPtr data;uint frames,flags;ulong position,qpc;OK(cap.GetBuffer(out data,out frames,out flags,out position,out qpc));if((flags&1)!=0)DiscontinuousPackets++;bool silent=(flags&2)!=0;if(!silent)Marshal.Copy(data,packet,0,(int)frames*2);float peak=0;
    for(int i=0;i<frames;i++){if(count==capFrames){read=(read+1)%capFrames;count--;};float l=silent?0:packet[i*2],r=silent?0:packet[i*2+1];ring[write*2]=l;ring[write*2+1]=r;stamps[write]=(flags&4)!=0?0:qpc+(ulong)(i*10000000.0/48000);peak=Math.Max(peak,Math.Max(Math.Abs(l),Math.Abs(r)));write=(write+1)%capFrames;count++;}MicPeak=Math.Max(MicPeak*.82f,peak);OK(cap.ReleaseBuffer(frames));OK(cap.GetNextPacketSize(out n));
   }
   uint padding;OK(output.Client.GetCurrentPadding(out padding));uint available=output.Buffer-padding;int block=(int)output.Period;if(available<output.Period)continue;if(count<block){if(padding==0&&(System.Diagnostics.Stopwatch.GetTimestamp()-origin)/qpcFrequency>1){if(!wasEmpty)StarvedBlocks++;wasEmpty=true;}continue;}wasEmpty=false;
   // Bound queued mic history; old data is discarded rather than adding delay.
   while(count>Math.Max(block*2,960)){read=(read+1)%capFrames;count--;}
   ulong firstStamp=stamps[read];for(int i=0;i<block;i++){raw[i*2]=ring[read*2];raw[i*2+1]=ring[read*2+1];read=(read+1)%capFrames;count--;}
   if(TestTone){for(int i=0;i<block;i++){float v=(float)(.05*Math.Sin(2*Math.PI*440*(generatedFrames+i)/48000));raw[i*2]=v;raw[i*2+1]=v;}generatedFrames+=block;}
   Effects(raw,wet,block);
   lock(recordingLock){if(dryFile!=null){dryFile.Write(raw,block);wetFile.Write(wet,block);}}
   if(Dry){float dryGain=(float)Math.Pow(10,Math.Max(-12,Math.Min(24,MicGainDb))/20);for(int i=0;i<block*2;i++)wet[i]=raw[i]*dryGain;}
   float target=Routing.ListenGain(Hear,VolumeDb),op=0,sp=0,songGain=(float)Math.Pow(10,Math.Max(-40,Math.Min(0,SongDb))/20);
   lock(songLock){for(int i=0;i<block*2;i++){float s=0;if(PlaySong&&song!=null){if(song.Position>=song.Duration){if(LoopSong)song.Rewind();else PlaySong=false;}if(PlaySong)s=song.Read()*songGain;}songBlock[i]=s;sp=Math.Max(sp,Math.Abs(s));}if(song!=null)SongPosition=song.Position;}SongPeak=sp;
   for(int i=0;i<block;i++){if(main!=null){if(mc==auxFrames){mr=(mr+1)%auxFrames;mc--;}mainRing[mw*2]=songBlock[i*2];mainRing[mw*2+1]=songBlock[i*2+1];mw=(mw+1)%auxFrames;mc++;}while(lc>2400){lr=(lr+1)%auxFrames;lc--;}for(int c=0;c<2;c++){float music=mirrorSystem?(lc>0?mirrorRing[lr*2+c]:0):songBlock[i*2+c];wet[i*2+c]=Routing.PrivateVoiceAndMusic(wet[i*2+c],music);}if(mirrorSystem&&lc>0){lr=(lr+1)%auxFrames;lc--;}}
   for(int i=0;i<block;i++){monitorGain+=(target-monitorGain)*.0021f;for(int c=0;c<2;c++){float v=Math.Max(-.501f,Math.Min(.501f,wet[i*2+c]))*monitorGain;sent[i*2+c]=v;op=Math.Max(op,Math.Abs(v));}}
   IntPtr dest;OK(render.GetBuffer((uint)block,out dest));Marshal.Copy(sent,0,dest,block*2);OK(render.ReleaseBuffer((uint)block,0));OutputPeak=op;RenderBlocks++;
   if(firstStamp>0){double now=System.Diagnostics.Stopwatch.GetTimestamp()*10000000.0/qpcFrequency;double age=(now-firstStamp)/10000.0+padding*1000.0/48000;if(age>=0&&age<500)QueueAgeMs=QueueAgeMs==0?age:QueueAgeMs*.95+age*.05;}
  }
 }catch(Exception e){Error=e.Message;}finally{Hear=false;Running=false;StopRecording();if(input!=null)input.Dispose();if(output!=null)output.Dispose();if(main!=null)main.Dispose();if(mirror!=null)mirror.Dispose();if(pitch!=null){pitch.Dispose();pitch=null;}if(scheduling!=IntPtr.Zero)AvRevertMmThreadCharacteristics(scheduling);}}
 public void Dispose(){if(disposed)return;Hear=false;stop.Set();if(thread!=null){if(!thread.Join(5000))throw new Exception("Audio thread did not close in time.");thread=null;}lock(songLock){if(song!=null){song.Dispose();song=null;}}stop.Dispose();disposed=true;}
}
}
