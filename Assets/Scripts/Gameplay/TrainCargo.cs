using UnityEngine;

namespace TrainTrain
{
    public sealed class TrainCargo
    {
        private readonly int[] counts = new int[4];

        public int Capacity { get; }
        public int Passengers { get; private set; }

        public TrainCargo(int capacity)
        {
            Capacity = Mathf.Max(1, capacity);
        }

        public RailColor? DominantColor
        {
            get
            {
                RailColor? result = null;
                int best = 0;
                for (int i = 0; i < counts.Length; i++)
                {
                    if (counts[i] > best)
                    {
                        best = counts[i];
                        result = (RailColor)i;
                    }
                }

                return result;
            }
        }

        public int CountOf(RailColor color)
        {
            return counts[(int)color];
        }

        public int UnloadMatching(RailColor color)
        {
            int count = counts[(int)color];
            counts[(int)color] = 0;
            Passengers -= count;
            return count;
        }

        public int Load(RailColor color, int available)
        {
            int loaded = Mathf.Clamp(Mathf.Min(available, Capacity - Passengers), 0, Capacity);
            counts[(int)color] += loaded;
            Passengers += loaded;
            return loaded;
        }
    }
}
