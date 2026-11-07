using System;
using Mirror;

namespace TomorinTrial
{
    public struct TrialPayloadPart : NetworkMessage
    {
        public byte kind;
        public int serial,index,total;
        public byte[] data;
    }
    // Large parties must not be disconnected by Telepathy's per-message limit.
    // Small rooms retain the normal messages; larger rosters and snapshots are split.
    public static class TrialPayloadParts
    {
        public static TrialPayloadPart[] Split<T>(T state,byte kind,int serial,int size=8192) where T:struct,NetworkMessage
        {
            using(var writer=NetworkWriterPool.Get())
            {
                writer.Write(state);var bytes=writer.ToArraySegment();int count=(bytes.Count+size-1)/size;
                var parts=new TrialPayloadPart[count];
                for(int i=0;i<count;i++)
                {
                    var data=new byte[Math.Min(size,bytes.Count-i*size)];Buffer.BlockCopy(bytes.Array,bytes.Offset+i*size,data,0,data.Length);
                    parts[i]=new TrialPayloadPart{kind=kind,serial=serial,index=i,total=count,data=data};
                }
                return parts;
            }
        }
    }
    public sealed class TrialPayloadJoiner
    {
        int serial,received,length;
        byte[][] parts;
        public void Reset(){parts=null;received=length=0;}
        public bool Add(TrialPayloadPart value,out byte[] payload)
        {
            payload=null;
            if(value.total<1 || value.total>4096 || value.index<0 || value.index>=value.total || value.data==null || value.data.Length>8192)return false;
            if(parts==null || serial!=value.serial){serial=value.serial;parts=new byte[value.total][];received=length=0;}
            if(parts.Length!=value.total || parts[value.index]!=null)return false;
            parts[value.index]=value.data;received++;length+=value.data.Length;
            if(received!=parts.Length)return false;
            payload=new byte[length];int offset=0;
            foreach(var part in parts){Buffer.BlockCopy(part,0,payload,offset,part.Length);offset+=part.Length;}
            parts=null;return true;
        }
    }
}
