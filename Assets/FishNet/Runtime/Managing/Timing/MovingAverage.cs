using FishNet.Documenting;
using System;
using System.Collections.Generic;


namespace FishNet.Managing.Timing
{

    [APIExclude]
    public class MovingAverage
    {
        #region Public.
        /// <summary>
        /// Average from samples favoring the most recent sample.
        /// </summary>
        public double Average { get; private set; }
        #endregion

        /// <summary>
        /// Next index to write a sample to.
        /// </summary>
        private int _writeIndex = 0;
        /// <summary>
        /// Collected samples.
        /// </summary>
        private double[] _samples;
        /// <summary>
        /// Number of samples written. Will be at most samples size.
        /// </summary>
        private int _writtenSamples = 0;
        /// <summary>
        /// Samples accumulated over queue.
        /// </summary>
        private double _sampleAccumulator;

        public MovingAverage(int sampleSize)
        {
            _samples = new double[sampleSize];
        }


        /// <summary>
        /// Computes a new windowed average each time a new sample arrives
        /// </summary>
        /// <param name="newSample"></param>
        public void ComputeAverage(double newSample)
        {
            if (newSample < 0d)
                newSample = 0d;

            _sampleAccumulator += newSample;
            _samples[_writeIndex] = newSample;

            //Increase writeIndex.
            _writeIndex++;
            _writtenSamples = Math.Max(_writtenSamples, _writeIndex);
            if (_writeIndex >= _samples.Length)
                _writeIndex = 0;
            /* If samples are full then drop off
             * the oldest sample. This will always be
             * the one just after written. The entry isn't
             * actually removed from the array but will
             * be overwritten next sample. */
            if (_writtenSamples >= _samples.Length)
                _sampleAccumulator -= _samples[_writeIndex];

            Average = _sampleAccumulator / _writtenSamples;
        }
    }


}