namespace Panaxeo
{
    public class Target
    {
        public enum TargetType
        {
            None = 0,
            PatrolBoat,//2
            Submarine,//3 (x2)
                      //Destroyer,//3
            Battleship,//4
            Carrier,//5
            Helicarrier//9
        }

        public TargetType Type { get; set; }

        public int GetSize
        {
            get
            {
                var result = 0;

                switch (Type)
                {
                    case TargetType.PatrolBoat:
                        result = 2;
                        break;
                    case TargetType.Submarine:
                        result = 3;
                        break;
                    case TargetType.Battleship:
                        result = 4;
                        break;
                    case TargetType.Carrier:
                        result = 5;
                        break;
                    case TargetType.Helicarrier:
                        result = 9;
                        break;
                }

                return result;
            }
        }

        public Target()
        {

        }

        public Target(int size)
        {
            switch (size)
            {
                case 2:
                    Type = TargetType.PatrolBoat;
                    break;
                case 3:
                    Type = TargetType.Submarine;
                    break;
                case 4:
                    Type = TargetType.Battleship;
                    break;
                case 5:
                    Type = TargetType.Carrier;
                    break;
                case 9:
                    Type = TargetType.Helicarrier;
                    break;
            }
        }


        public static List<Target> GetAllPossibleTargets =>
            new List<Target>()
            {
                new Target(){ Type = TargetType.PatrolBoat },
                new Target(){ Type = TargetType.Submarine },
                new Target(){ Type = TargetType.Submarine },
                new Target(){ Type = TargetType.Battleship },
                new Target(){ Type = TargetType.Carrier },
                //new Target(){ Type = TargetType.Helicarrier }
            };

        public static int GetMinLeghtOfAvailableTarget(List<Target> existingTargets, bool avengerFound)
        {
            var allRemainingTargets = GetAllPossibleTargets;

            if (
                existingTargets.Count(i => i.Type == TargetType.PatrolBoat) > 1 ||
                existingTargets.Count(i => i.Type == TargetType.Submarine) > 2 && existingTargets.Any(i => i.Type == TargetType.Helicarrier) ||
                existingTargets.Count(i => i.Type == TargetType.Battleship) > 1 && existingTargets.Any(i => i.Type == TargetType.Helicarrier) ||
                existingTargets.Count(i => i.Type == TargetType.Carrier) > 1 && existingTargets.Any(i => i.Type == TargetType.Helicarrier)
                )
            {
                throw new ArgumentOutOfRangeException();
            }

            if (existingTargets.Any(i => i.Type == TargetType.PatrolBoat))
            {
                allRemainingTargets = allRemainingTargets.Where(i => i.Type != TargetType.PatrolBoat).ToList();
            }

            if (existingTargets.Where(i => i.Type == TargetType.Submarine).Count() == 2)
            {
                allRemainingTargets = allRemainingTargets.Where(i => i.Type != TargetType.Submarine).ToList();
            }
            else if (existingTargets.Where(i => i.Type == TargetType.Submarine).Count() == 1)
            {
                allRemainingTargets.Remove(allRemainingTargets.First(i => i.Type == TargetType.Submarine));
            }

            if (existingTargets.Any(i => i.Type == TargetType.Carrier))
            {
                allRemainingTargets = allRemainingTargets.Where(i => i.Type != TargetType.Carrier).ToList();
            }

            if (existingTargets.Any(i => i.Type == TargetType.Battleship))
            {
                allRemainingTargets = allRemainingTargets.Where(i => i.Type != TargetType.Battleship).ToList();
            }

            if (avengerFound)
            {
                allRemainingTargets.Where(i => i.Type != TargetType.Helicarrier).ToList();
            }

            //if (!avengerFound)
            //{
            //    return 3;
            //}

            var result = allRemainingTargets.DefaultIfEmpty(new Target()).Min(i => i.GetSize);

            return result > 3 && !avengerFound ? 3 : result;
        }

        public static int GetMaxLeghtOfAvailableTarget(List<Target> existingTargets, bool avengerFound)
        {
            var allRemainingTargets = GetAllPossibleTargets;

            if (existingTargets.Any(i => i.Type == TargetType.PatrolBoat))
            {
                allRemainingTargets = allRemainingTargets.Where(i => i.Type != TargetType.PatrolBoat).ToList();
            }

            if (existingTargets.Where(i => i.Type == TargetType.Submarine).Count() == 2)
            {
                allRemainingTargets = allRemainingTargets.Where(i => i.Type != TargetType.Submarine).ToList();
            }
            else if (existingTargets.Where(i => i.Type == TargetType.Submarine).Count() == 1)
            {
                allRemainingTargets.Remove(allRemainingTargets.First(i => i.Type == TargetType.Submarine));
            }

            if (existingTargets.Any(i => i.Type == TargetType.Carrier))
            {
                allRemainingTargets = allRemainingTargets.Where(i => i.Type != TargetType.Carrier).ToList();
            }

            if (existingTargets.Any(i => i.Type == TargetType.Battleship))
            {
                allRemainingTargets = allRemainingTargets.Where(i => i.Type != TargetType.Battleship).ToList();
            }

            if (avengerFound)
            {
                allRemainingTargets.Where(i => i.Type != TargetType.Helicarrier).ToList();
            }

            if (!avengerFound)
            {
                return 5;
            }

            return allRemainingTargets.DefaultIfEmpty(new Target()).Max(i => i.GetSize);
        }
    }
}