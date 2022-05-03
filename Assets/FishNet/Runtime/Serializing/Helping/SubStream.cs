namespace FishNet.Serializing.Helping
{
    /// <summary>
    /// Substream enabled direct writing & reading of data, and can be used in RPCs (as argument) or Broadcasts (as variable in struct).
    /// </summary>
    public struct SubStream
    {
        [System.NonSerialized]
        internal PooledWriter sWriter;
        [System.NonSerialized]
        internal PooledSubReader sReader;

        /// <summary>
        /// Use this to start reading from SubStream. It works only inside RPC function or on receiving Broadcast.
        /// </summary>
        /// <returns>Reader to read data from. No lenght/remaining checking (has to be manual by user).</returns>
        public PooledSubReader GetReader() => sReader;

        //public Writer GetWriter() => sWriter;

        /// <summary>
        /// Use this before writing and then calling RPC/sending broadcast:
        /// </summary>
        /// <param name="ss">Use this as an parameter for calling RPC function OR variable in Broadcast struct before sending.</param>
        /// <returns>Writer for writing data.</returns>
        public static Writer GetWriteStream(out SubStream ss)
        {
            ss = new SubStream();
            ss.sWriter = WriterPool.GetWriter();
            return ss.sWriter;
        }

        /// <summary>
        /// You have to call this after you have finished reading from SubStream. It recycles internal buffer into pool.
        /// If you fail to use this, it will incur GC costs.
        /// </summary>
        /// <exception cref="System.Exception">Throws error in case of wrong use.</exception>
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
