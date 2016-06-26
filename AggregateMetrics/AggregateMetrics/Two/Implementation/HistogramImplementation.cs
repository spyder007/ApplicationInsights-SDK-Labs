﻿namespace Microsoft.ApplicationInsights.Extensibility.AggregateMetrics.Two
{
    using System;
    using System.Threading;
    using Microsoft.ApplicationInsights.DataContracts;

    internal class HistogramImplementation : NamedCounterValueBase, ICounterValue, IHistogram
    {
        private long compositeValue;

        private int minValue = Int32.MaxValue;
        private int maxValue = Int32.MinValue;

        public HistogramImplementation(string name, TelemetryContext context)
            : base(name, context)
        {
        }

        private static void InterlockedExchangeOnCondition(ref int location, int value, Func<int, int, bool> condition)
        {
            int current = location;

            while (condition(current, value))
            {
                var previous = Interlocked.CompareExchange(ref location, value, current);

                // In most cases first condition will break the loop. 
                // Sometimes another thread may set other value. Than we need to retry
                if (previous == current || !condition(previous, value))
                    break;
                
                current = location;
            }
        }

        public MetricTelemetry Value
        {
            get 
            {
                var metric = this.GetInitializedMetricTelemetry();

                var curCompositeValue = Interlocked.Read(ref this.compositeValue);
                int curMinValue = this.minValue;
                int curMaxValue = this.maxValue;

                var count = (int)(curCompositeValue & ((1 << 24) - 1));
                double value = curCompositeValue >> 24;

                if (count != 0)
                {
                    metric.Value = value / count;
                    metric.Count = count;
                    metric.Min = curMinValue;
                    metric.Max = curMaxValue;
                }
                else
                {
                    metric.Value = 0;
                    metric.Count = 0;
                }

                return metric;
            }
        }

        public MetricTelemetry GetValueAndReset()
        {
            long curValue = Interlocked.Exchange(ref this.compositeValue, 0);
            int curMinValue = this.minValue;
            int curMaxValue = this.maxValue;

            minValue = Int32.MaxValue;
            maxValue = Int32.MinValue;


            var count = (int)(curValue & ((1 << 24) - 1));
            double value = curValue >> 24;

            var metric = this.GetInitializedMetricTelemetry();
            if (count != 0)
            {
                metric.Value = value / count;
                metric.Count = count;
                metric.Min = curMinValue;
                metric.Max = curMaxValue;
            }
            else
            {
                metric.Value = 0;
                metric.Count = 0;
            }

            return metric;
        }

        public void Update(int value)
        {
            long delta = ((value) << 24) + 1;
            Interlocked.Add(ref this.compositeValue, delta);
            InterlockedExchangeOnCondition(ref this.minValue, value, (currentValue, newValue) => { return currentValue > newValue; });
            InterlockedExchangeOnCondition(ref this.maxValue, value, (currentValue, newValue) => { return currentValue < newValue; });
        }
    }
}
