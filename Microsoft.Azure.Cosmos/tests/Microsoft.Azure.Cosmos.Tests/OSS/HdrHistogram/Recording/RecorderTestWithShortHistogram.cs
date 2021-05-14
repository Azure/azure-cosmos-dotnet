// This file isn't generated, but this comment is necessary to exclude it from StyleCop analysis.
// <auto-generated/>

using Xunit;

namespace HdrHistogram.UnitTests.Recording
{
    
    internal sealed class RecorderTestWithShortHistogram : RecorderTestsBase
    {
        internal override HistogramBase CreateHistogram(long id, long min, long max, int sf)
        {
            //return new ShortHistogram(id, min, max, sf);
            return HistogramFactory.With16BitBucketSize()
                .WithValuesFrom(min)
                .WithValuesUpTo(max)
                .WithPrecisionOf(sf)
                .Create();
        }

        internal override Recorder Create(long min, long max, int sf)
        {
            return HistogramFactory.With16BitBucketSize()
                .WithValuesFrom(min)
                .WithValuesUpTo(max)
                .WithPrecisionOf(sf)
                .WithThreadSafeReads()
                .Create();
        }
    }
}