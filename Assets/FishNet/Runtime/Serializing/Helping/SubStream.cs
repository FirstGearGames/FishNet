namespace FishNet.Serializing.Helping
{
    public struct SubStream
    {
        [System.NonSerialized]
        internal PooledSubWriter sWriter;
        [System.NonSerialized]
        internal PooledSubReader sReader;

        public PooledSubReader GetReader() => sReader;
        public Writer GetWriter() => sWriter;
    
        public static Writer GetWriteStream(out SubStream ss)
        {
            ss = new SubStream();
            ss.sWriter = SubWriterPool.GetSubWriter();
            return ss.sWriter;
        }

        public void RecycleAfterRead()
        {
            if(sReader != null)
            {
                sReader.Dispose();
                sReader = null;
            }
            else
            {
                throw new System.Exception("Reader inside SubStream doesn't exist; it may be already recycled or it wasn't used correctly!");
            }
        }
    }

}
