using System.Data.Common;
using System.Drawing;
using System.Linq;
using static Panaxeo.MapAnalysis;
using static Panaxeo.MapPoint;
using static Panaxeo.Program;

namespace Panaxeo
{
    public class Map
    {
        public static int ReanalyzeMapsCounter { get; set; }

        public static List<MapAnalyzer> Statistics { get; set; }

        public List<MapPoint> Points { get; set; }

        public List<Target> Targets { get; set; }

        public bool AvengerFound { get; set; }

        public int MapId { get; set; }

        public Map(FireResponse fireResponse)
        {
            Points = new List<MapPoint>();
            Targets = new List<Target>();
            MapId = fireResponse.MapId;

            for (var row = 0; row < Settings.GRID_SIZE; row++)
            {
                for (int column = 0; column < Settings.GRID_SIZE; column++)
                {
                    Points.Add(new MapPoint(row, column, fireResponse.Grid[column + row * Settings.GRID_SIZE]));
                }
            }
        }

        internal MapPoint GetTargetPoint()
        {
            var validPoints = Points.Where(i => i.Type == MapPoint.MapPointType.CalculatedPossibleTarget).ToList();

            if (Statistics == null)
            {
                Statistics = new List<MapAnalyzer>();
            }

            if (ReanalyzeMapsCounter <= 0 && Settings.ENABLE_STATISTICS)
            {
                ReanalyzeMapsCounter = 10;

                Statistics = MapAnalysis.AnalyzeMaps()
                    .Join(Points.Where(i => i.Type == MapPointType.CalculatedPossibleTarget || i.Type == MapPointType.Unknown),
                    o => o.Index,
                    i => i.Index,
                    (o, i) => o)
                    .ToList()
                    ;
            }

            ReanalyzeMapsCounter--;

            if (!validPoints.Any() && Settings.ENABLE_STATISTICS)
            {
                var avg = Statistics.Sum(i => i.Count) / Statistics.Count;

                var topRecords = ((Statistics.Max(i => i.Count) - avg) / 2) + avg;
                var bottomRecords = avg / 2;

                Statistics.Join
                    (Points,
                    i => i.Index,
                    o => o.Index,
                    (o, i) => new { analyzedPoints = o, validPoints = i })
                    .ToList()
                    .ForEach(i =>
                    {
                        //if(i.validPoints.ValidForTripleCheckedGrid)
                        //{
                            //i.validPoints.MaxPossibleCrossTargets +=
                            //i.analyzedPoints.Count >= topRecords ? 2 :
                            ////i.analyzedPoints.Count <= bottomRecords ? -1 :
                            //i.analyzedPoints.Count < avg ? 0 :
                            //1;

                            i.validPoints.MaxPossibleCrossTargets +=
                            i.analyzedPoints.Count >= topRecords ? 1 :
                            //i.analyzedPoints.Count <= bottomRecords ? -1 :
                            i.analyzedPoints.Count <= ((avg/3)*2) ? -1 :
                            0;
                        //}
                    });

            }

            if (!validPoints.Any())
            {
                //validPoints = Points.Where(i => i.Type == MapPoint.MapPointType.Unknown && !i.IsCorner && i.ValidForCheckedGrid).ToList();
                validPoints = Points.Where(i => i.Type == MapPoint.MapPointType.Unknown).ToList();


                //most 3 points
                //var max3CrossTargets = validPoints
                //    .Select(i => i.MaxPossibleCrossTargets)
                //    .Distinct()
                //    .OrderByDescending(i => i)
                //    .Take(3)
                //    .Last();
                ////.ToList();

                //validPoints = validPoints.Where(i => i.MaxPossibleCrossTargets >= max3CrossTargets).ToList();

                var maxCross = validPoints.Max(i => i.MaxPossibleCrossTargets);
                validPoints = validPoints.Where(i => i.MaxPossibleCrossTargets == maxCross).ToList();

                var maxAdjacent = validPoints.Max(i => i.AllAdjacentTargetCount);
                validPoints = validPoints.Where(i => i.AllAdjacentTargetCount == maxAdjacent).ToList();

                if (validPoints.Any(i => i.ValidForTripleCheckedGrid))
                {
                    validPoints = validPoints.Where(i => i.ValidForTripleCheckedGrid).ToList();
                }

                //if (validPoints.Any(i => !i.IsCorner))
                //{
                //    validPoints = validPoints.Where(i => !i.IsCorner).ToList();
                //}


                //var list = Program.AnalyzeMaps();

                //list.Join
                //    (validPoints,
                //    i => i.Index,
                //    o => o.Index,
                //    (o, i) => new { analyzedPoints = o, validPoints = i })
                //.OrderBy(i => i.analyzedPoints.Count)
                //.Take((validPoints.Count() / 3) * 2)
                //.Select(i => i.validPoints)
                //.ToList()
                //.ForEach(i => { validPoints.Remove(i); })
                //;




                //var orderedList = list.OrderBy(i => i.Count).ToList();

                //foreach (var item in orderedList)
                //{
                //    if (Points.Single(i => i.Index == item.Index).Type == MapPointType.Unknown)
                //    {
                //        Points.Single(i => i.Index == item.Index).CurrentMinShipSize = Target.GetMinLeghtOfAvailableTarget(Targets, false);

                //        return Points.Single(i => i.Index == item.Index);
                //    }
                //}

            }
            else
            {
                //avenger priority
                if (validPoints.Any(i => i.IsAvengerTarget))
                {
                    validPoints = validPoints.Where(i => i.IsAvengerTarget).ToList();
                }
                else
                {
                    var maxCount = validPoints.Max(i => i.AllAdjacentTargetCount);
                    validPoints = validPoints.Where(i => i.AllAdjacentTargetCount == maxCount).ToList();

                    var statisticsPoints = validPoints.Join(Statistics,
                        i => i.Index,
                        o => o.Index,
                        (o, i) => new { result = o, value = i.Count }
                        )
                        .OrderByDescending(i => i.value)
                        .Take(1)
                        .Select(i => i.result)
                        .ToList();

                    validPoints = statisticsPoints.Any() ? statisticsPoints : validPoints;
                }
            }

            var index = new Random().Next(validPoints.Count);
            var result = validPoints[index];

            result.CurrentMinShipSize = Target.GetMinLeghtOfAvailableTarget(Targets, AvengerFound);

            return result;
        }

        internal void CalculatePossibleTargets()
        {
            SetBoundariesForIronManTarget();

            FindBasicShips();

            CheckAdjacentEmptyCells();

            CalculateAdjacentEmptyCells();

            if (!AvengerFound)
            {
                FindAvengerShip();
            }

            if (AvengerFound)
            {
                MarkAvengerShip();
            }

            CalculateAdjacentEmptyCells();

            CheckAdjacentEmptyCells();

            MapTargets();

            RemoveCalculatedPossibleTargetsBasedOnAlreadyDestroyedTargets();

            CalculateAdjacentEmptyCells();

            RemoveUnknownCellsWhereRemainingDoNotFit();

            CheckAdjacentEmptyCells();

            MarkPossibleTargetsWithAdjacentCount();

            CalculatePossibleCrossTargets();

            //CalcuateScoreOfEmptyTargets();

            //AddCalculationOfTripleChecked();
        }

        private void MapTargets()
        {
            if (AvengerFound)
            {
                var avengerPoints = Points.Where(i => i.Type == MapPointType.Target && i.IsAvengerTarget);

                if (avengerPoints.Count() == 9)
                {
                    Targets.Add(new Target(9));
                }
            }

            MarkTargetsInner();
        }

        private void MarkTargetsInner()
        {
            var anythingMapped = false;

            Points
                .Where(i => i.Type == MapPointType.Target && !i.IsAvengerTarget)
                .ToList()
                .ForEach(point =>
                {
                    if (point.AlreadyMapped)
                    {
                        return;
                    }

                    var countRight = GetCountOfAdjacentTargetsInDirection(point, MapPointerPosition.Right);
                    var countLeft = GetCountOfAdjacentTargetsInDirection(point, MapPointerPosition.Left);
                    var countTop = GetCountOfAdjacentTargetsInDirection(point, MapPointerPosition.Top);
                    var countBottom = GetCountOfAdjacentTargetsInDirection(point, MapPointerPosition.Bottom);

                    var terminatedRight = IsLastAdjacentCellEmptyInDirection(point, MapPointerPosition.Right);
                    var terminatedLeft = IsLastAdjacentCellEmptyInDirection(point, MapPointerPosition.Left);
                    var terminatedTop = IsLastAdjacentCellEmptyInDirection(point, MapPointerPosition.Top);
                    var terminatedBottom = IsLastAdjacentCellEmptyInDirection(point, MapPointerPosition.Bottom);

                    var markTopBottom = countTop + countBottom > 0 && terminatedTop && terminatedBottom;
                    var markLeftRight = countRight + countLeft > 0 && terminatedRight && terminatedLeft;

                    if (!markTopBottom && countTop + countBottom + 1 == 4 &&
                        Targets.Any(i => i.Type == Target.TargetType.Carrier))
                    {
                        markTopBottom = true;
                    }

                    if (!markTopBottom && countTop + countBottom + 1 == 3 &&
                        Targets.Any(i => i.Type == Target.TargetType.Carrier) &&
                        Targets.Any(i => i.Type == Target.TargetType.Battleship)
                        )
                    {
                        markTopBottom = true;
                    }

                    if (!markTopBottom && countTop + countBottom + 1 == 2 &&
                        Targets.Any(i => i.Type == Target.TargetType.Carrier) &&
                        Targets.Any(i => i.Type == Target.TargetType.Battleship) &&
                        Targets.Count(i => i.Type == Target.TargetType.Submarine) == 2
                        )
                    {
                        markTopBottom = true;
                    }

                    if (!markLeftRight && countLeft + countRight + 1 == 4 &&
                        Targets.Any(i => i.Type == Target.TargetType.Carrier))
                    {
                        markLeftRight = true;
                    }

                    if (!markLeftRight && countLeft + countRight + 1 == 3 &&
                        Targets.Any(i => i.Type == Target.TargetType.Carrier) &&
                        Targets.Any(i => i.Type == Target.TargetType.Battleship)
                        )
                    {
                        markLeftRight = true;
                    }

                    if (!markLeftRight && countLeft + countRight + 1 == 2 &&
                        Targets.Any(i => i.Type == Target.TargetType.Carrier) &&
                        Targets.Any(i => i.Type == Target.TargetType.Battleship) &&
                        Targets.Count(i => i.Type == Target.TargetType.Submarine) == 2
                        )
                    {
                        markLeftRight = true;
                    }

                    if (markTopBottom)
                    {
                        Targets.Add(new Target(countTop + countBottom + 1));
                        point.AlreadyMapped = true;

                        for (var i = 0; i < countTop; i++)
                        {
                            GetPoint(point.X - i - 1, point.Y).AlreadyMapped = true;
                        }

                        for (var i = 0; i < countBottom; i++)
                        {
                            GetPoint(point.X + i + 1, point.Y).AlreadyMapped = true;
                        }

                        anythingMapped = true;
                    }

                    if (markLeftRight)
                    {
                        Targets.Add(new Target(countRight + countLeft + 1));
                        point.AlreadyMapped = true;

                        for (var i = 0; i < countRight; i++)
                        {
                            GetPoint(point.X, point.Y + i + 1).AlreadyMapped = true;
                        }

                        for (var i = 0; i < countLeft; i++)
                        {
                            GetPoint(point.X, point.Y - i - 1).AlreadyMapped = true;
                        }

                        anythingMapped = true;
                    }
                });

            if(anythingMapped)
            {
                MarkTargetsInner();
            }
        }

        private void AddCalculationOfTripleChecked()
        {
            Points
                .Where(i => i.Type == MapPointType.Unknown)
                .ToList()
                .ForEach(point =>
                {
                    if(point.ValidForTripleCheckedGrid)
                    {
                        point.MaxPossibleCrossTargets += 1;
                    }
                });
        }

        private void CalculatePossibleCrossTargets()
        {
            var smallestMissingSize = Target.GetMinLeghtOfAvailableTarget(Targets, AvengerFound);
            var largestMissingSize = Target.GetMaxLeghtOfAvailableTarget(Targets, AvengerFound);

            Points
                .Where(i => i.Type == MapPointType.Unknown)
                .ToList()
                .ForEach(point =>
                {

                    var countRight = GetCountOfAdjacentPossibleTargetsInDirection(point, MapPointerPosition.Right);
                    var countLeft = GetCountOfAdjacentPossibleTargetsInDirection(point, MapPointerPosition.Left);
                    var countTop = GetCountOfAdjacentPossibleTargetsInDirection(point, MapPointerPosition.Top);
                    var countBottom = GetCountOfAdjacentPossibleTargetsInDirection(point, MapPointerPosition.Bottom);


                    //OutputMap();
                    var countVertical = countTop + countBottom + 1;
                    var countHorizontal = countLeft + countRight + 1;

                    if (countVertical < smallestMissingSize &&
                       countHorizontal < smallestMissingSize
                    )
                    {
                        point.Type = MapPointType.CalculatedEmpty;
                    }


                    point.MaxPossibleCrossTargets = 0;

                    var directions = MapPointerPosition.NotSet;

                    if (countVertical >= smallestMissingSize && countBottom > 0 && countTop > 0)
                    {
                        point.MaxPossibleCrossTargets += GetMaxAvailableSize(countVertical, largestMissingSize) - smallestMissingSize + 1;
                        directions |= MapPointerPosition.Top | MapPointerPosition.Bottom;
                    }

                    if (countHorizontal >= smallestMissingSize && countRight > 0 && countLeft > 0)
                    {
                        point.MaxPossibleCrossTargets += GetMaxAvailableSize(countHorizontal, largestMissingSize) - smallestMissingSize + 1;
                        directions |= MapPointerPosition.Right | MapPointerPosition.Left;
                    }

                    if (countRight + 1 >= smallestMissingSize)
                    {
                        point.MaxPossibleCrossTargets += GetMaxAvailableSize(countRight + 1, largestMissingSize) - smallestMissingSize + 1;
                        directions |= MapPointerPosition.Right;

                    }

                    if (countLeft + 1 >= smallestMissingSize)
                    {
                        point.MaxPossibleCrossTargets += GetMaxAvailableSize(countLeft + 1, largestMissingSize) - smallestMissingSize + 1;
                        directions |= MapPointerPosition.Left;
                    }

                    if (countTop + 1 >= smallestMissingSize)
                    {
                        point.MaxPossibleCrossTargets += GetMaxAvailableSize(countTop + 1, largestMissingSize) - smallestMissingSize + 1;

                        directions |= MapPointerPosition.Top;
                    }

                    if (countBottom + 1 >= smallestMissingSize)
                    {
                        point.MaxPossibleCrossTargets += GetMaxAvailableSize(countBottom + 1, largestMissingSize) - smallestMissingSize + 1;

                        directions |= MapPointerPosition.Bottom;
                    }

                    if((directions & (MapPointerPosition.Bottom | MapPointerPosition.Top)) != MapPointerPosition.NotSet &&
                    (directions & (MapPointerPosition.Right | MapPointerPosition.Left)) != MapPointerPosition.NotSet)
                    {
                        point.MaxPossibleCrossTargets += 3;
                    }
                });

            //calculate for avenger

            var avengerScore = 5;

            if (!AvengerFound)
            {
                Points
                    .Where(i => i.Type == MapPointType.Unknown)
                    .ToList()
                    .ForEach(point =>
                    {
                        var countRight = GetCountOfAdjacentPossibleTargetsInDirection(point, MapPointerPosition.Right);
                        var countLeft = GetCountOfAdjacentPossibleTargetsInDirection(point, MapPointerPosition.Left);
                        var countTop = GetCountOfAdjacentPossibleTargetsInDirection(point, MapPointerPosition.Top);
                        var countBottom = GetCountOfAdjacentPossibleTargetsInDirection(point, MapPointerPosition.Bottom);

                        if (countRight >= 4 &&
                            GetPoint(point.X + 1, point.Y + 1).IsUnknown &&
                            GetPoint(point.X - 1, point.Y + 1).IsUnknown &&
                            GetPoint(point.X + 1, point.Y + 3).IsUnknown &&
                            GetPoint(point.X - 1, point.Y + 3).IsUnknown)
                        {
                            point.MaxPossibleCrossTargets += avengerScore;
                            GetPoint(point.X, point.Y + 1).MaxPossibleCrossTargets += avengerScore;
                            GetPoint(point.X, point.Y + 2).MaxPossibleCrossTargets += avengerScore;
                            GetPoint(point.X, point.Y + 3).MaxPossibleCrossTargets += avengerScore;
                            GetPoint(point.X, point.Y + 4).MaxPossibleCrossTargets += avengerScore;

                            GetPoint(point.X + 1, point.Y + 1).MaxPossibleCrossTargets += avengerScore;
                            GetPoint(point.X - 1, point.Y + 1).MaxPossibleCrossTargets += avengerScore;
                            GetPoint(point.X + 1, point.Y + 3).MaxPossibleCrossTargets += avengerScore;
                            GetPoint(point.X - 1, point.Y + 3).MaxPossibleCrossTargets += avengerScore;
                        }


                        if (countBottom >= 4 &&
                            GetPoint(point.X + 1, point.Y + 1).IsUnknown &&
                            GetPoint(point.X + 1, point.Y - 1).IsUnknown &&
                            GetPoint(point.X + 3, point.Y + 1).IsUnknown &&
                            GetPoint(point.X + 3, point.Y - 1).IsUnknown)
                        {
                            point.MaxPossibleCrossTargets += avengerScore;
                            GetPoint(point.X + 1, point.Y).MaxPossibleCrossTargets += avengerScore;
                            GetPoint(point.X + 2, point.Y).MaxPossibleCrossTargets += avengerScore;
                            GetPoint(point.X + 3, point.Y).MaxPossibleCrossTargets += avengerScore;
                            GetPoint(point.X + 4, point.Y).MaxPossibleCrossTargets += avengerScore;

                            GetPoint(point.X + 1, point.Y + 1).MaxPossibleCrossTargets += avengerScore;
                            GetPoint(point.X + 1, point.Y - 1).MaxPossibleCrossTargets += avengerScore;
                            GetPoint(point.X + 3, point.Y + 1).MaxPossibleCrossTargets += avengerScore;
                            GetPoint(point.X + 3, point.Y - 1).MaxPossibleCrossTargets += avengerScore;
                        }
                    });
            }
        }

        private void SetBoundariesForIronManTarget()
        {
            var ironManTarget = Settings.LoadFile(MapId);

            if (ironManTarget != null)
            {
                var point = GetPoint(ironManTarget.X, ironManTarget.Y);

                var countRight = GetCountOfAdjacentTargetsInDirection(point, MapPointerPosition.Right);
                var countLeft = GetCountOfAdjacentTargetsInDirection(point, MapPointerPosition.Left);
                var countTop = GetCountOfAdjacentTargetsInDirection(point, MapPointerPosition.Top);
                var countBottom = GetCountOfAdjacentTargetsInDirection(point, MapPointerPosition.Bottom);

                var countVertical = countTop + countBottom + 1;
                var countHorizontal = countLeft + countRight + 1;

                if (countVertical == ironManTarget.ShipSize)
                {
                    GetPoint(point.X, point.Y - 1).Type = MapPointType.CalculatedEmpty;
                    GetPoint(point.X, point.Y + 1).Type = MapPointType.CalculatedEmpty;

                    GetPoint(point.X - countTop - 1, point.Y + 1).Type = MapPointType.CalculatedEmpty;
                    GetPoint(point.X - countTop - 1, point.Y).Type = MapPointType.CalculatedEmpty;
                    GetPoint(point.X - countTop - 1, point.Y - 1).Type = MapPointType.CalculatedEmpty;

                    GetPoint(point.X + countBottom + 1, point.Y + 1).Type = MapPointType.CalculatedEmpty;
                    GetPoint(point.X + countBottom + 1, point.Y).Type = MapPointType.CalculatedEmpty;
                    GetPoint(point.X + countBottom + 1, point.Y - 1).Type = MapPointType.CalculatedEmpty;

                    for (var i = 0; i < countTop; i++)
                    {
                        GetPoint(point.X - i - 1, point.Y + 1).Type = MapPointType.CalculatedEmpty;
                        GetPoint(point.X - i - 1, point.Y - 1).Type = MapPointType.CalculatedEmpty;
                    }

                    for (var i = 0; i < countBottom; i++)
                    {
                        GetPoint(point.X + i + 1, point.Y + 1).Type = MapPointType.CalculatedEmpty;
                        GetPoint(point.X + i + 1, point.Y - 1).Type = MapPointType.CalculatedEmpty;
                    }
                }

                if (countHorizontal == ironManTarget.ShipSize)
                {
                    GetPoint(point.X - 1, point.Y).Type = MapPointType.CalculatedEmpty;
                    GetPoint(point.X + 1, point.Y).Type = MapPointType.CalculatedEmpty;

                    GetPoint(point.X + 1, point.Y - countLeft - 1).Type = MapPointType.CalculatedEmpty;
                    GetPoint(point.X, point.Y - countLeft - 1).Type = MapPointType.CalculatedEmpty;
                    GetPoint(point.X - 1, point.Y - countLeft - 1).Type = MapPointType.CalculatedEmpty;

                    GetPoint(point.X + 1, point.Y + countRight + 1).Type = MapPointType.CalculatedEmpty;
                    GetPoint(point.X, point.Y + countRight + 1).Type = MapPointType.CalculatedEmpty;
                    GetPoint(point.X - 1, point.Y + countRight + 1).Type = MapPointType.CalculatedEmpty;

                    for (var i = 0; i < countLeft; i++)
                    {
                        GetPoint(point.X + 1, point.Y - i - 1).Type = MapPointType.CalculatedEmpty;
                        GetPoint(point.X - 1, point.Y - i - 1).Type = MapPointType.CalculatedEmpty;
                    }

                    for (var i = 0; i < countBottom; i++)
                    {
                        GetPoint(point.X + 1, point.Y + i + 1).Type = MapPointType.CalculatedEmpty;
                        GetPoint(point.X - 1, point.Y + i + 1).Type = MapPointType.CalculatedEmpty;
                    }
                }
            }

        }

        private void MarkPossibleTargetsWithAdjacentCount()
        {
            Points
                .Where(i => i.Type == MapPointType.CalculatedPossibleTarget || i.Type == MapPointType.Target)
                .ToList()
                .ForEach(point =>
                {
                    var countRight = GetCountOfAdjacentPossibleTargetsInDirection(point, MapPointerPosition.Right);
                    var countLeft = GetCountOfAdjacentPossibleTargetsInDirection(point, MapPointerPosition.Left);
                    var countTop = GetCountOfAdjacentPossibleTargetsInDirection(point, MapPointerPosition.Top);
                    var countBottom = GetCountOfAdjacentPossibleTargetsInDirection(point, MapPointerPosition.Bottom);

                    var minShipSize = Target.GetMinLeghtOfAvailableTarget(Targets, AvengerFound);

                    if(countRight + countLeft + 1 >= minShipSize &&
                        (GetCountOfAdjacentTargetsInDirection(point, MapPointerPosition.Right) >= 1 ||
                        GetCountOfAdjacentTargetsInDirection(point, MapPointerPosition.Left) >= 1)
                    )
                    {
                        point.AllAdjacentTargetCount = countRight + countLeft;
                    }

                    if (countTop + countBottom + 1 >= minShipSize &&
                        (GetCountOfAdjacentTargetsInDirection(point, MapPointerPosition.Top) >= 1 ||
                        GetCountOfAdjacentTargetsInDirection(point, MapPointerPosition.Bottom) >= 1)
                    )
                    {
                        point.AllAdjacentTargetCount = countBottom + countTop;
                    }


                    //point.AllAdjacentTargetCount = countRight + countLeft + countTop + countBottom;
                });
        }

        private void RemoveCalculatedPossibleTargetsBasedOnAlreadyDestroyedTargets()
        {
            //throw new NotImplementedException();

            if (Targets.Any(i => i.Type == Target.TargetType.Carrier) &&
                Targets.Any(i => i.Type == Target.TargetType.Helicarrier))
            {
                CalculateMaxTargetSize(4);
            }

            if (Targets.Any(i => i.Type == Target.TargetType.Carrier) &&
                Targets.Any(i => i.Type == Target.TargetType.Helicarrier) &&
                Targets.Any(i => i.Type == Target.TargetType.Battleship))
            {
                CalculateMaxTargetSize(3);
            }


            if (Targets.Any(i => i.Type == Target.TargetType.Carrier) &&
                Targets.Any(i => i.Type == Target.TargetType.Helicarrier) &&
                Targets.Any(i => i.Type == Target.TargetType.Battleship) &&
                Targets.Count(i => i.Type == Target.TargetType.Submarine) == 2
                )
            {
                CalculateMaxTargetSize(2);
            }
        }

        private void CalculateMaxTargetSize(int size)
        {
            Points
                    .Where(i => i.Type == MapPointType.Target)
                    .ToList()
                    .ForEach(point =>
                    {
                        var countRight = GetCountOfAdjacentTargetsInDirection(point, MapPointerPosition.Right);
                        var countBottom = GetCountOfAdjacentTargetsInDirection(point, MapPointerPosition.Bottom);

                        if (countRight + 1 == size)
                        {
                            MarkPointAsCalculatedEmpty(GetPoint(point.X, point.Y - 1), true);
                            MarkPointAsCalculatedEmpty(GetPoint(point.X, point.Y + countRight + 1), true);
                        }

                        if (countBottom + 1 == size)
                        {
                            MarkPointAsCalculatedEmpty(GetPoint(point.X - 1, point.Y), true);
                            MarkPointAsCalculatedEmpty(GetPoint(point.X + countBottom + 1, point.Y), true);
                        }
                    });
        }

        private void RemoveUnknownCellsWhereRemainingDoNotFit()
        {
            var minShipSizeRemaining = Target.GetMinLeghtOfAvailableTarget(Targets, AvengerFound);

            Points
               .Where(i => i.Type == MapPointType.Unknown)
               .ToList()
               .ForEach(point =>
               {
                   var countRight = GetCountOfAdjacentPossibleTargetsInDirection(point, MapPointerPosition.Right);
                   var countLeft = GetCountOfAdjacentPossibleTargetsInDirection(point, MapPointerPosition.Left);
                   var countTop = GetCountOfAdjacentPossibleTargetsInDirection(point, MapPointerPosition.Top);
                   var countBottom = GetCountOfAdjacentPossibleTargetsInDirection(point, MapPointerPosition.Bottom);

                   if (countRight + countLeft + 1 < minShipSizeRemaining &&
                        countBottom + countTop + 1 < minShipSizeRemaining)
                   {
                       point.Type = MapPointType.CalculatedEmpty;
                   }
               });
        }


        private int GetMaxAvailableSize(int countFree, int maxMissingSize)
        {
            countFree = countFree > maxMissingSize ? maxMissingSize : countFree;

            return countFree;
        }

        //private int GetMaxAvailableSize(int countFree, int maxMissingSize, int verticalSpace, int horizontalSpace)
        //{
        //    countFree = countFree > maxMissingSize ? maxMissingSize : countFree;

        //    var minSize = horizontalSpace > verticalSpace ? horizontalSpace : verticalSpace;


        //    return countFree >= minSize ? countFree : 0;
        //}

        private void CalculateAdjacentEmptyCells()
        {
            Points
                .Where(i => i.Type == MapPointType.Target)
                .ToList()
                .ForEach(point =>
                {
                    var countRight = GetCountOfAdjacentTargetsInDirection(point, MapPointerPosition.Right);
                    var countLeft = GetCountOfAdjacentTargetsInDirection(point, MapPointerPosition.Left);
                    var countTop = GetCountOfAdjacentTargetsInDirection(point, MapPointerPosition.Top);
                    var countBottom = GetCountOfAdjacentTargetsInDirection(point, MapPointerPosition.Bottom);

                    var terminatedRight = IsLastAdjacentCellEmptyInDirection(point, MapPointerPosition.Right);
                    var terminatedLeft = IsLastAdjacentCellEmptyInDirection(point, MapPointerPosition.Left);
                    var terminatedTop = IsLastAdjacentCellEmptyInDirection(point, MapPointerPosition.Top);
                    var terminatedBottom = IsLastAdjacentCellEmptyInDirection(point, MapPointerPosition.Bottom);

                    if (countTop + countBottom == Settings.MAX_SHIP_LENGTH - 1 ||
                        countLeft + countRight == Settings.MAX_SHIP_LENGTH - 1
                    )
                    {
                        MarkPointAsCalculatedEmpty(GetPoint(point.X - 1, point.Y - 1));
                        MarkPointAsCalculatedEmpty(GetPoint(point.X - 1, point.Y));
                        MarkPointAsCalculatedEmpty(GetPoint(point.X - 1, point.Y + 1));

                        MarkPointAsCalculatedEmpty(GetPoint(point.X, point.Y - 1));
                        MarkPointAsCalculatedEmpty(GetPoint(point.X, point.Y + 1));

                        MarkPointAsCalculatedEmpty(GetPoint(point.X + 1, point.Y - 1));
                        MarkPointAsCalculatedEmpty(GetPoint(point.X + 1, point.Y));
                        MarkPointAsCalculatedEmpty(GetPoint(point.X + 1, point.Y + 1));
                    }

                });


            Points
                .Where(i => i.Type == MapPointType.Target)
                .ToList()
                .ForEach(point =>
                {
                    var countRight = GetCountOfAdjacentTargetsInDirection(point, MapPointerPosition.Right);
                    var countLeft = GetCountOfAdjacentTargetsInDirection(point, MapPointerPosition.Left);
                    var countTop = GetCountOfAdjacentTargetsInDirection(point, MapPointerPosition.Top);
                    var countBottom = GetCountOfAdjacentTargetsInDirection(point, MapPointerPosition.Bottom);

                    var terminatedRight = IsLastAdjacentCellEmptyInDirection(point, MapPointerPosition.Right);
                    var terminatedLeft = IsLastAdjacentCellEmptyInDirection(point, MapPointerPosition.Left);
                    var terminatedTop = IsLastAdjacentCellEmptyInDirection(point, MapPointerPosition.Top);
                    var terminatedBottom = IsLastAdjacentCellEmptyInDirection(point, MapPointerPosition.Bottom);

                    if (countTop + countBottom > 0 && terminatedTop && terminatedBottom && !point.IsAvengerTarget ||
                        countLeft + countRight > 0 && terminatedLeft && terminatedRight && !point.IsAvengerTarget
                    )
                    {
                        MarkPointAsCalculatedEmpty(GetPoint(point.X - 1, point.Y - 1));
                        MarkPointAsCalculatedEmpty(GetPoint(point.X - 1, point.Y));
                        MarkPointAsCalculatedEmpty(GetPoint(point.X - 1, point.Y + 1));

                        MarkPointAsCalculatedEmpty(GetPoint(point.X, point.Y - 1));
                        MarkPointAsCalculatedEmpty(GetPoint(point.X, point.Y + 1));

                        MarkPointAsCalculatedEmpty(GetPoint(point.X + 1, point.Y - 1));
                        MarkPointAsCalculatedEmpty(GetPoint(point.X + 1, point.Y));
                        MarkPointAsCalculatedEmpty(GetPoint(point.X + 1, point.Y + 1));
                    }

                });
        }

        private void FindAvengerShip()
        {
            Points
                .Where(i => i.Type == MapPointType.Target)
                .ToList()
                .ForEach(point =>
                {
                    var countRight = GetCountOfAdjacentTargetsInDirection(point, MapPointerPosition.Right);
                    var countLeft = GetCountOfAdjacentTargetsInDirection(point, MapPointerPosition.Left);
                    var countTop = GetCountOfAdjacentTargetsInDirection(point, MapPointerPosition.Top);
                    var countBottom = GetCountOfAdjacentTargetsInDirection(point, MapPointerPosition.Bottom);


                    if (countBottom == Settings.MAX_SHIP_LENGTH - 1)
                    {
                        if (GetPoint(point.X + 1, point.Y + 1).PotentialAvengerTarget &&
                        GetPoint(point.X + 1, point.Y - 1).PotentialAvengerTarget &&
                        GetPoint(point.X + 3, point.Y - 1).PotentialAvengerTarget &&
                        GetPoint(point.X + 3, point.Y + 1).PotentialAvengerTarget
                            )
                        {
                            var possibleAvengerPoint = GetPoint(point.X + 1, point.Y + 1);
                            possibleAvengerPoint.Type = MapPointType.CalculatedPossibleTarget;
                            possibleAvengerPoint.IsAvengerTarget = true;
                        }
                    }

                    if (countRight == Settings.MAX_SHIP_LENGTH - 1)
                    {
                        if (GetPoint(point.X + 1, point.Y + 1).PotentialAvengerTarget &&
                            GetPoint(point.X - 1, point.Y + 1).PotentialAvengerTarget &&
                            GetPoint(point.X - 1, point.Y + 3).PotentialAvengerTarget &&
                            GetPoint(point.X + 1, point.Y + 3).PotentialAvengerTarget
                            )
                        {
                            var possibleAvengerPoint = GetPoint(point.X + 1, point.Y + 1);
                            possibleAvengerPoint.Type = MapPointType.CalculatedPossibleTarget;
                            possibleAvengerPoint.IsAvengerTarget = true;
                        }
                    }

                });

            //need to check, if there is enough space on sides, when checking the 3 points

            //check for length 3 avenger ship
            if (!Points.Any(i => i.Type == MapPointType.CalculatedPossibleTarget && i.IsAvengerTarget))
            {
                Points
                    .Where(i => i.Type == MapPointType.Target)
                    .ToList()
                    .ForEach(point =>
                    {
                        var countRight = GetCountOfAdjacentTargetsInDirection(point, MapPointerPosition.Right);
                        var countLeft = GetCountOfAdjacentTargetsInDirection(point, MapPointerPosition.Left);
                        var countTop = GetCountOfAdjacentTargetsInDirection(point, MapPointerPosition.Top);
                        var countBottom = GetCountOfAdjacentTargetsInDirection(point, MapPointerPosition.Bottom);

                        var terminatedRight = IsLastAdjacentCellEmptyInDirection(point, MapPointerPosition.Right);
                        var terminatedLeft = IsLastAdjacentCellEmptyInDirection(point, MapPointerPosition.Left);
                        var terminatedTop = IsLastAdjacentCellEmptyInDirection(point, MapPointerPosition.Top);
                        var terminatedBottom = IsLastAdjacentCellEmptyInDirection(point, MapPointerPosition.Bottom);

                        if (countBottom == 2 &&
                            countTop == 0 &&
                            terminatedTop &&
                            terminatedBottom &&
                            GetPoint(point.X + 1, point.Y + 1).PotentialAvengerTarget &&
                            GetPoint(point.X + 1, point.Y - 1).PotentialAvengerTarget
                            )
                        {
                            var centerPoint = GetPoint(point.X + 1, point.Y);
                            var centerPointCountRight = GetCountOfAdjacentValidTargetInDirection(centerPoint, MapPointerPosition.Right);
                            var centerPointCountLeft = GetCountOfAdjacentValidTargetInDirection(centerPoint, MapPointerPosition.Left);

                            if (centerPointCountRight >= 1 && centerPointCountLeft >= 3 ||
                            centerPointCountRight >= 3 && centerPointCountLeft >= 1
                            )
                            {
                                var possibleAvengerPoint = GetPoint(point.X + 1, point.Y + 1);
                                possibleAvengerPoint.Type = MapPointType.CalculatedPossibleTarget;
                                possibleAvengerPoint.IsAvengerTarget = true;
                            }
                        }

                        if (countRight == 2 &&
                            countLeft == 0 &&
                            terminatedLeft &&
                            terminatedRight &&
                            GetPoint(point.X + 1, point.Y + 1).PotentialAvengerTarget &&
                            GetPoint(point.X - 1, point.Y + 1).PotentialAvengerTarget
                            )
                        {

                            var centerPoint = GetPoint(point.X, point.Y + 1);
                            var centerPointCountTop = GetCountOfAdjacentValidTargetInDirection(centerPoint, MapPointerPosition.Top);
                            var centerPointCountBottom = GetCountOfAdjacentValidTargetInDirection(centerPoint, MapPointerPosition.Bottom);

                            if (centerPointCountTop >= 1 && centerPointCountBottom >= 3 ||
                            centerPointCountTop >= 3 && centerPointCountBottom >= 1
                            )
                            {
                                var possibleAvengerPoint = GetPoint(point.X + 1, point.Y + 1);
                                possibleAvengerPoint.Type = MapPointType.CalculatedPossibleTarget;
                                possibleAvengerPoint.IsAvengerTarget = true;
                            }
                        }

                    });
            }
        }

        private void MarkAvengerShip()
        {
            Points
                .ForEach(point =>
                {
                    if (point.Type == MapPointType.Target)
                    {
                        var countRight = GetCountOfAdjacentTargetsInDirection(point, MapPointerPosition.Right);
                        var countLeft = GetCountOfAdjacentTargetsInDirection(point, MapPointerPosition.Left);
                        var countTop = GetCountOfAdjacentTargetsInDirection(point, MapPointerPosition.Top);
                        var countBottom = GetCountOfAdjacentTargetsInDirection(point, MapPointerPosition.Bottom);

                        if (AvengerFound)
                        {
                            //middle part of avenger
                            if (countTop + countBottom > 0 && countLeft + countRight > 0)
                            {
                                if (countTop + countBottom == Settings.MAX_SHIP_LENGTH - 1)
                                {
                                    var startingPoint = GetPoint(point.X - countTop, point.Y);

                                    //mark targets as Avenger
                                    MarkAsAvengerTarget(startingPoint);
                                    MarkAsAvengerTarget(GetPoint(startingPoint.X + 1, startingPoint.Y));
                                    MarkAsAvengerTarget(GetPoint(startingPoint.X + 2, startingPoint.Y));
                                    MarkAsAvengerTarget(GetPoint(startingPoint.X + 3, startingPoint.Y));
                                    MarkAsAvengerTarget(GetPoint(startingPoint.X + 4, startingPoint.Y));



                                    //targets - wings
                                    MarkAsAvengerTarget(MarkPointAsPossibleTargetIfUnknown(GetPoint(startingPoint.X + 1, startingPoint.Y + 1)));
                                    MarkAsAvengerTarget(MarkPointAsPossibleTargetIfUnknown(GetPoint(startingPoint.X + 1, startingPoint.Y - 1)));
                                    MarkAsAvengerTarget(MarkPointAsPossibleTargetIfUnknown(GetPoint(startingPoint.X + 3, startingPoint.Y + 1)));
                                    MarkAsAvengerTarget(MarkPointAsPossibleTargetIfUnknown(GetPoint(startingPoint.X + 3, startingPoint.Y - 1)));

                                    //remove unknown cells around avenger
                                    MarkPointAsCalculatedEmpty(GetPoint(startingPoint.X - 1, startingPoint.Y), true);
                                    MarkPointAsCalculatedEmpty(GetPoint(startingPoint.X - 1, startingPoint.Y - 1), true);
                                    MarkPointAsCalculatedEmpty(GetPoint(startingPoint.X - 1, startingPoint.Y + 1), true);

                                    MarkPointAsCalculatedEmpty(GetPoint(startingPoint.X, startingPoint.Y - 1), true);
                                    MarkPointAsCalculatedEmpty(GetPoint(startingPoint.X, startingPoint.Y + 1), true);
                                    MarkPointAsCalculatedEmpty(GetPoint(startingPoint.X, startingPoint.Y + 2), true);
                                    MarkPointAsCalculatedEmpty(GetPoint(startingPoint.X, startingPoint.Y - 2), true);

                                    MarkPointAsCalculatedEmpty(GetPoint(startingPoint.X + 1, startingPoint.Y + 2), true);
                                    MarkPointAsCalculatedEmpty(GetPoint(startingPoint.X + 1, startingPoint.Y - 2), true);

                                    MarkPointAsCalculatedEmpty(GetPoint(startingPoint.X + 2, startingPoint.Y - 1), true);
                                    MarkPointAsCalculatedEmpty(GetPoint(startingPoint.X + 2, startingPoint.Y + 1), true);
                                    MarkPointAsCalculatedEmpty(GetPoint(startingPoint.X + 2, startingPoint.Y + 2), true);
                                    MarkPointAsCalculatedEmpty(GetPoint(startingPoint.X + 2, startingPoint.Y - 2), true);

                                    MarkPointAsCalculatedEmpty(GetPoint(startingPoint.X + 3, startingPoint.Y + 2), true);
                                    MarkPointAsCalculatedEmpty(GetPoint(startingPoint.X + 3, startingPoint.Y - 2), true);

                                    MarkPointAsCalculatedEmpty(GetPoint(startingPoint.X + 4, startingPoint.Y - 1), true);
                                    MarkPointAsCalculatedEmpty(GetPoint(startingPoint.X + 4, startingPoint.Y + 1), true);
                                    MarkPointAsCalculatedEmpty(GetPoint(startingPoint.X + 4, startingPoint.Y + 2), true);
                                    MarkPointAsCalculatedEmpty(GetPoint(startingPoint.X + 4, startingPoint.Y - 2), true);

                                    MarkPointAsCalculatedEmpty(GetPoint(startingPoint.X + 5, startingPoint.Y), true);
                                    MarkPointAsCalculatedEmpty(GetPoint(startingPoint.X + 5, startingPoint.Y - 1), true);
                                    MarkPointAsCalculatedEmpty(GetPoint(startingPoint.X + 5, startingPoint.Y + 1), true);

                                }

                                if (countLeft + countRight == Settings.MAX_SHIP_LENGTH - 1)
                                {
                                    var startingPoint = GetPoint(point.X, point.Y - countLeft);

                                    //mark targets as Avenger
                                    MarkAsAvengerTarget(startingPoint);
                                    MarkAsAvengerTarget(GetPoint(startingPoint.X, startingPoint.Y + 1));
                                    MarkAsAvengerTarget(GetPoint(startingPoint.X, startingPoint.Y + 2));
                                    MarkAsAvengerTarget(GetPoint(startingPoint.X, startingPoint.Y + 3));
                                    MarkAsAvengerTarget(GetPoint(startingPoint.X, startingPoint.Y + 4));


                                    //targets - wings
                                    MarkAsAvengerTarget(MarkPointAsPossibleTargetIfUnknown(GetPoint(startingPoint.X + 1, startingPoint.Y + 1)));
                                    MarkAsAvengerTarget(MarkPointAsPossibleTargetIfUnknown(GetPoint(startingPoint.X - 1, startingPoint.Y + 1)));
                                    MarkAsAvengerTarget(MarkPointAsPossibleTargetIfUnknown(GetPoint(startingPoint.X + 1, startingPoint.Y + 3)));
                                    MarkAsAvengerTarget(MarkPointAsPossibleTargetIfUnknown(GetPoint(startingPoint.X - 1, startingPoint.Y + 3)));

                                    //remove unknown cells around avenger
                                    MarkPointAsCalculatedEmpty(GetPoint(startingPoint.X, startingPoint.Y - 1), true);
                                    MarkPointAsCalculatedEmpty(GetPoint(startingPoint.X + 1, startingPoint.Y - 1), true);
                                    MarkPointAsCalculatedEmpty(GetPoint(startingPoint.X - 1, startingPoint.Y - 1), true);

                                    MarkPointAsCalculatedEmpty(GetPoint(startingPoint.X - 1, startingPoint.Y), true);
                                    MarkPointAsCalculatedEmpty(GetPoint(startingPoint.X + 1, startingPoint.Y), true);
                                    MarkPointAsCalculatedEmpty(GetPoint(startingPoint.X + 2, startingPoint.Y), true);
                                    MarkPointAsCalculatedEmpty(GetPoint(startingPoint.X - 2, startingPoint.Y), true);

                                    MarkPointAsCalculatedEmpty(GetPoint(startingPoint.X + 2, startingPoint.Y + 1), true);
                                    MarkPointAsCalculatedEmpty(GetPoint(startingPoint.X - 2, startingPoint.Y + 1), true);

                                    MarkPointAsCalculatedEmpty(GetPoint(startingPoint.X - 1, startingPoint.Y + 2), true);
                                    MarkPointAsCalculatedEmpty(GetPoint(startingPoint.X + 1, startingPoint.Y + 2), true);
                                    MarkPointAsCalculatedEmpty(GetPoint(startingPoint.X + 2, startingPoint.Y + 2), true);
                                    MarkPointAsCalculatedEmpty(GetPoint(startingPoint.X - 2, startingPoint.Y + 2), true);

                                    MarkPointAsCalculatedEmpty(GetPoint(startingPoint.X + 2, startingPoint.Y + 3), true);
                                    MarkPointAsCalculatedEmpty(GetPoint(startingPoint.X - 2, startingPoint.Y + 3), true);

                                    MarkPointAsCalculatedEmpty(GetPoint(startingPoint.X - 1, startingPoint.Y + 4), true);
                                    MarkPointAsCalculatedEmpty(GetPoint(startingPoint.X + 1, startingPoint.Y + 4), true);
                                    MarkPointAsCalculatedEmpty(GetPoint(startingPoint.X + 2, startingPoint.Y + 4), true);
                                    MarkPointAsCalculatedEmpty(GetPoint(startingPoint.X - 2, startingPoint.Y + 4), true);

                                    MarkPointAsCalculatedEmpty(GetPoint(startingPoint.X, startingPoint.Y + 5), true);
                                    MarkPointAsCalculatedEmpty(GetPoint(startingPoint.X - 1, startingPoint.Y + 5), true);
                                    MarkPointAsCalculatedEmpty(GetPoint(startingPoint.X + 1, startingPoint.Y + 5), true);
                                }

                                if (countTop == 2 && countBottom == 1)
                                {
                                    MarkPointAsCalculatedEmpty(GetPoint(point.X + 2, point.Y), true);
                                }

                                if (countTop == 1 && countBottom == 2)
                                {
                                    MarkPointAsCalculatedEmpty(GetPoint(point.X - 2, point.Y), true);
                                }

                                if (countLeft == 1 && countRight == 2)
                                {
                                    MarkPointAsCalculatedEmpty(GetPoint(point.X, point.Y - 2), true);
                                }

                                if (countLeft == 2 && countRight == 1)
                                {
                                    MarkPointAsCalculatedEmpty(GetPoint(point.X, point.Y + 2), true);
                                }
                            }
                            
                        }
                    }
                });
        }

        private void FindBasicShips()
        {
            Points
                .ForEach(point =>
                {
                    if (point.Type == MapPointType.Empty || point.Type == MapPointType.CalculatedEmpty)
                    {
                        return;
                    }

                    if (point.Type == MapPointType.Target)
                    {

                        var countRight = GetCountOfAdjacentTargetsInDirection(point, MapPointerPosition.Right);
                        var countLeft = GetCountOfAdjacentTargetsInDirection(point, MapPointerPosition.Left);
                        var countTop = GetCountOfAdjacentTargetsInDirection(point, MapPointerPosition.Top);
                        var countBottom = GetCountOfAdjacentTargetsInDirection(point, MapPointerPosition.Bottom);

                        var terminatedRight = IsLastAdjacentCellEmptyInDirection(point, MapPointerPosition.Right);
                        var terminatedLeft = IsLastAdjacentCellEmptyInDirection(point, MapPointerPosition.Left);
                        var terminatedTop = IsLastAdjacentCellEmptyInDirection(point, MapPointerPosition.Top);
                        var terminatedBottom = IsLastAdjacentCellEmptyInDirection(point, MapPointerPosition.Bottom);

                        //if no adjacent cells are targets, then mark all adjacent cells as possible targets
                        if (countRight + countLeft + countTop + countBottom == 0)
                        {
                            MarkAdjacentPointAsPossibleTargetIfUnknown(point, MapPointerPosition.Bottom);
                            MarkAdjacentPointAsPossibleTargetIfUnknown(point, MapPointerPosition.Top);
                            MarkAdjacentPointAsPossibleTargetIfUnknown(point, MapPointerPosition.Left);
                            MarkAdjacentPointAsPossibleTargetIfUnknown(point, MapPointerPosition.Right);
                            return;
                        }

                        if (countTop == 0 && countBottom == 0)
                        {
                            if (countLeft + countRight < Settings.MAX_SHIP_LENGTH - 1)
                            {
                                if (terminatedLeft && !terminatedRight)
                                {
                                    GetAdjacentUnknownPoint(point, MapPointerPosition.Right, countRight + 1).Type = MapPointType.CalculatedPossibleTarget;
                                }
                                else if (!terminatedLeft && terminatedRight)
                                {
                                    GetAdjacentUnknownPoint(point, MapPointerPosition.Left, countLeft + 1).Type = MapPointType.CalculatedPossibleTarget;
                                }
                                else if (!terminatedLeft && !terminatedRight)
                                {
                                    GetAdjacentUnknownPoint(point, MapPointerPosition.Right, countRight + 1).Type = MapPointType.CalculatedPossibleTarget;
                                    GetAdjacentUnknownPoint(point, MapPointerPosition.Left, countLeft + 1).Type = MapPointType.CalculatedPossibleTarget;

                                }
                            }
                        }

                        if (countLeft == 0 && countRight == 0)
                        {
                            if (countTop + countBottom < Settings.MAX_SHIP_LENGTH - 1)
                            {
                                if (terminatedTop && !terminatedBottom)
                                {
                                    GetAdjacentUnknownPoint(point, MapPointerPosition.Bottom, countBottom + 1).Type = MapPointType.CalculatedPossibleTarget;
                                }
                                else if (!terminatedTop && terminatedBottom)
                                {
                                    GetAdjacentUnknownPoint(point, MapPointerPosition.Top, countTop + 1).Type = MapPointType.CalculatedPossibleTarget;
                                }
                                else if (!terminatedTop && !terminatedBottom)
                                {
                                    GetAdjacentUnknownPoint(point, MapPointerPosition.Bottom, countBottom + 1).Type = MapPointType.CalculatedPossibleTarget;
                                    GetAdjacentUnknownPoint(point, MapPointerPosition.Top, countTop + 1).Type = MapPointType.CalculatedPossibleTarget;

                                }
                            }
                        }

                        if (countTop + countBottom > 0 && countLeft + countRight > 0)
                        {
                            AvengerFound = true;
                            MarkAsAvengerTarget(GetPoint(point.X, point.Y + 1));
                            MarkAsAvengerTarget(GetPoint(point.X, point.Y - 1));
                            MarkAsAvengerTarget(GetPoint(point.X + 1, point.Y));
                            MarkAsAvengerTarget(GetPoint(point.X - 1, point.Y));
                        }




                    }

                });
        }

        private void CheckAdjacentEmptyCells()
        {
            Points
                .ForEach(point =>
                {
                    if (point.Type == MapPointType.Empty || point.Type == MapPointType.CalculatedEmpty)
                    {
                        return;
                    }

                    //if all around are empty, then it will be empty as well

                    //corners
                    if (point.Position.HasFlag(MapPointerPosition.CornerTopLeft))
                    {
                        if (GetAdjacentPoint(point, MapPointerPosition.Bottom).IsEmpty && GetAdjacentPoint(point, MapPointerPosition.Right).IsEmpty)
                        {
                            point.Type = MapPointType.CalculatedEmpty;
                        }
                        return;
                    }

                    if (point.Position.HasFlag(MapPointerPosition.CornerTopRight))
                    {
                        if (GetAdjacentPoint(point, MapPointerPosition.Bottom).IsEmpty && GetAdjacentPoint(point, MapPointerPosition.Left).IsEmpty)
                        {
                            point.Type = MapPointType.CalculatedEmpty;
                        }
                        return;
                    }

                    if (point.Position.HasFlag(MapPointerPosition.CornerBottomLeft))
                    {
                        if (GetAdjacentPoint(point, MapPointerPosition.Top).IsEmpty && GetAdjacentPoint(point, MapPointerPosition.Right).IsEmpty)
                        {
                            point.Type = MapPointType.CalculatedEmpty;
                        }
                        return;
                    }

                    if (point.Position.HasFlag(MapPointerPosition.CornerBottomRight))
                    {
                        if (GetAdjacentPoint(point, MapPointerPosition.Top).IsEmpty && GetAdjacentPoint(point, MapPointerPosition.Left).IsEmpty)
                        {
                            point.Type = MapPointType.CalculatedEmpty;
                        }
                        return;
                    }

                    if (point.Position.HasFlag(MapPointerPosition.CornerBottomRight))
                    {
                        if (GetAdjacentPoint(point, MapPointerPosition.Top).IsEmpty && GetAdjacentPoint(point, MapPointerPosition.Left).IsEmpty)
                        {
                            point.Type = MapPointType.CalculatedEmpty;
                        }
                        return;
                    }


                    //sides
                    if (point.Position.HasFlag(MapPointerPosition.Top))
                    {
                        if (GetAdjacentPoint(point, MapPointerPosition.Left).IsEmpty && GetAdjacentPoint(point, MapPointerPosition.Right).IsEmpty && GetAdjacentPoint(point, MapPointerPosition.Bottom).IsEmpty)
                        {
                            point.Type = MapPointType.CalculatedEmpty;
                        }
                        return;
                    }

                    if (point.Position.HasFlag(MapPointerPosition.Bottom))
                    {
                        if (GetAdjacentPoint(point, MapPointerPosition.Left).IsEmpty && GetAdjacentPoint(point, MapPointerPosition.Right).IsEmpty && GetAdjacentPoint(point, MapPointerPosition.Top).IsEmpty)
                        {
                            point.Type = MapPointType.CalculatedEmpty;
                        }
                        return;
                    }

                    if (point.Position.HasFlag(MapPointerPosition.Left))
                    {
                        if (GetAdjacentPoint(point, MapPointerPosition.Bottom).IsEmpty && GetAdjacentPoint(point, MapPointerPosition.Right).IsEmpty && GetAdjacentPoint(point, MapPointerPosition.Top).IsEmpty)
                        {
                            point.Type = MapPointType.CalculatedEmpty;
                        }
                        return;
                    }

                    if (point.Position.HasFlag(MapPointerPosition.Right))
                    {
                        if (GetAdjacentPoint(point, MapPointerPosition.Bottom).IsEmpty && GetAdjacentPoint(point, MapPointerPosition.Left).IsEmpty && GetAdjacentPoint(point, MapPointerPosition.Top).IsEmpty)
                        {
                            point.Type = MapPointType.CalculatedEmpty;
                        }
                        return;
                    }

                    //middle

                    if (point.Position.HasFlag(MapPointerPosition.Middle))
                    {
                        if (GetAdjacentPoint(point, MapPointerPosition.Bottom).IsEmpty && GetAdjacentPoint(point, MapPointerPosition.Left).IsEmpty && GetAdjacentPoint(point, MapPointerPosition.Top).IsEmpty && GetAdjacentPoint(point, MapPointerPosition.Right).IsEmpty)
                        {
                            point.Type = MapPointType.CalculatedEmpty;
                        }
                        return;
                    }


                });
        }

        public MapPoint GetAdjacentPoint(MapPoint point, MapPointerPosition position, int length = 1)
        {
            var result = new MapPoint();

            switch (position)
            {
                case MapPointerPosition.Top:
                    result = GetPoint(point.X - length, point.Y);
                    break;
                case MapPointerPosition.Bottom:
                    result = GetPoint(point.X + length, point.Y);
                    break;
                case MapPointerPosition.Left:
                    result = GetPoint(point.X, point.Y - length);
                    break;
                case MapPointerPosition.Right:
                    result = GetPoint(point.X, point.Y + length);
                    break;
            }

            return result;
        }

        public MapPoint GetAdjacentUnknownPoint(MapPoint point, MapPointerPosition position, int length = 1)
        {
            var result = GetAdjacentPoint(point, position, length);

            return result.Type == MapPointType.Unknown ? result : new MapPoint();
        }

        public void MarkAdjacentPointAsPossibleTargetIfUnknown(MapPoint point, MapPointerPosition position)
        {
            var resultPoint = GetAdjacentPoint(point, position);

            if (resultPoint.Type == MapPointType.Unknown)
            {
                resultPoint.Type = MapPointType.CalculatedPossibleTarget;
            }
        }

        public MapPoint MarkPointAsPossibleTargetIfUnknown(MapPoint point)
        {
            if (point.Type == MapPointType.Unknown || point.Type == MapPointType.CalculatedEmpty)
            {
                point.Type = MapPointType.CalculatedPossibleTarget;
            }

            return point;
        }

        public MapPoint MarkAsAvengerTarget(MapPoint point)
        {
            point.IsAvengerTarget = true;
            point.AlreadyMapped = true;

            return point;
        }

        public void MarkPointAsCalculatedEmpty(MapPoint point, bool forceCalculatedTarget = false)
        {
            if (point.Type == MapPointType.Unknown || point.Type == MapPointType.CalculatedPossibleTarget && (forceCalculatedTarget || !point.IsAvengerTarget))
            {
                point.Type = MapPointType.CalculatedEmpty;
            }
        }

        public int GetCountOfAdjacentTargetsInDirection(MapPoint point, MapPointerPosition position)
        {
            var result = 0;
            var adjacent = GetAdjacentPoint(point, position);

            if (adjacent.IsTarget)
            {
                result++;
                result += GetCountOfAdjacentTargetsInDirection(adjacent, position);
            }

            return result;
        }

        public int GetCountOfAdjacentPossibleTargetsInDirection(MapPoint point, MapPointerPosition position)
        {
            var result = 0;
            var adjacent = GetAdjacentPoint(point, position);

            if (adjacent.Type == MapPointType.Target || adjacent.Type == MapPointType.CalculatedPossibleTarget || adjacent.Type == MapPointType.Unknown)
            {
                result++;
                result += GetCountOfAdjacentPossibleTargetsInDirection(adjacent, position);
            }

            return result;
        }

        public int GetCountOfAdjacentUnknownInDirection(MapPoint point, MapPointerPosition position)
        {
            var result = 0;
            var adjacent = GetAdjacentPoint(point, position);

            if (adjacent.IsUnknown)
            {
                result++;
                result += GetCountOfAdjacentUnknownInDirection(adjacent, position);
            }

            return result;
        }

        public int GetCountOfAdjacentValidTargetInDirection(MapPoint point, MapPointerPosition position)
        {
            var result = 0;
            var adjacent = GetAdjacentPoint(point, position);

            if (adjacent.PotentialAvengerTarget)
            {
                result++;
                result += GetCountOfAdjacentValidTargetInDirection(adjacent, position);
            }

            return result;
        }

        public bool IsLastAdjacentCellEmptyInDirection(MapPoint point, MapPointerPosition position)
        {
            var adjacent = GetAdjacentPoint(point, position);

            if (adjacent.IsEmpty || adjacent.Type == MapPointType.Outside)
            {
                return true;
            }

            if (adjacent.IsTarget)
            {
                return IsLastAdjacentCellEmptyInDirection(adjacent, position);
            }

            return false;
        }

        //private void CalcuateScoreOfEmptyTargets()
        //{
        //    //probably not needed
        //    Points
        //        .Where(i => i.Type == MapPoint.MapPointType.Unknown)
        //        .ToList()
        //        .ForEach(point =>
        //        {
        //            if(point.X > 0 && GetPoint(point.X - 1, point.Y).Type == MapPoint.MapPointType.Unknown)
        //            {
        //                point.TargetingScore++;
        //            }

        //            if()
        //        })
        //}

        public MapPoint GetPoint(int row, int column)
        {
            return Points.SingleOrDefault(i => i.X == row && i.Y == column) ?? new MapPoint();
        }

        public enum MapOutputType
        {
            Normal = 0,
            TargetMatchScore,
            Checked,
        }

        public void OutputMap(MapOutputType type = MapOutputType.Normal)
        {
            foreach (var point in Points)
            {
                var value = (char)point.Type;

                if (type == MapOutputType.TargetMatchScore && point.Type == MapPointType.Unknown)
                {
                    value = point.MaxPossibleCrossTargets <= 9 ? (char)(48 + point.MaxPossibleCrossTargets) : (char)(97 + point.MaxPossibleCrossTargets - 10);
                }

                if(type == MapOutputType.Checked)
                {
                    value = point.ValidForTripleCheckedGrid ? 'X' : '.';
                }

                Console.Write(value);

                if (point.Y == Settings.GRID_SIZE - 1)
                {
                    Console.Write("\r\n");
                }
            }

            Console.WriteLine(string.Empty);
            Console.WriteLine(string.Empty);
            Console.WriteLine(string.Empty);
        }
    }

    public class MapPoint
    {
        public enum MapPointType
        {
            Unknown = '*',
            Empty = '.',
            Target = 'X',
            CalculatedPossibleTarget = 'o',
            CalculatedEmpty = ',',
            Outside = '?',
            Fire = '~',
        }

        public enum MapPointPriority
        {
            None = 0,
            Possible,
            High
        }

        public enum MapPointAvenger
        {
            None = 0,
            hulk,
            ironman,
            thor
        }

        public enum MapPointerPosition
        {
            NotSet = 0,
            Middle = 1,
            Top = 2,
            Bottom = 4,
            Left = 8,
            Right = 16,
            CornerTopLeft = 32,
            CornerTopRight = 64,
            CornerBottomLeft = 128,
            CornerBottomRight = 256,
            Corner = 512
        }


        public int X { get; set; }

        public int Y { get; set; }

        public MapPointType Type { get; set; }

        public MapPointPriority Priority { get; set; }//needed?

        public List<Target> PossibleTargets { get; set; }

        public int TargetingScore { get; set; }

        public MapPointAvenger UseAvenger { get; set; }

        public bool IsAvengerTarget { get; set; }

        public int AllAdjacentTargetCount { get; set; }

        public bool AlreadyMapped { get; set; }

        public int MaxPossibleCrossTargets { get; set; }

        public int CurrentMinShipSize { get; set; }

        public int Index => Y + X * Settings.GRID_SIZE;

        public MapPointerPosition Position =>
            X == 0 && Y == 0 ? MapPointerPosition.CornerTopLeft | MapPointerPosition.Top | MapPointerPosition.Left | MapPointerPosition.Corner :
                X == 0 && Y == Settings.GRID_SIZE - 1 ? MapPointerPosition.CornerTopRight | MapPointerPosition.Top | MapPointerPosition.Right | MapPointerPosition.Corner :
                X == Settings.GRID_SIZE - 1 && Y == 0 ? MapPointerPosition.CornerBottomLeft | MapPointerPosition.Bottom | MapPointerPosition.Left | MapPointerPosition.Corner :
                X == Settings.GRID_SIZE - 1 && Y == Settings.GRID_SIZE - 1 ? MapPointerPosition.CornerBottomRight | MapPointerPosition.Bottom | MapPointerPosition.Right | MapPointerPosition.Corner :
                X == 0 ? MapPointerPosition.Top :
                X == Settings.GRID_SIZE - 1 ? MapPointerPosition.Bottom :
                Y == 0 ? MapPointerPosition.Left :
                Y == Settings.GRID_SIZE - 1 ? MapPointerPosition.Right :
                MapPointerPosition.Middle;

        public bool IsCorner => Position.HasFlag(MapPointerPosition.Corner);

        public bool IsEmpty => Type == MapPointType.Empty || Type == MapPointType.CalculatedEmpty;

        public bool IsTarget => Type == MapPointType.Target;

        public bool PotentialAvengerTarget => Type == MapPointType.Target || Type == MapPointType.CalculatedEmpty || Type == MapPointType.Unknown;

        public bool IsUnknown => Type == MapPointType.Unknown || Type == MapPointType.CalculatedPossibleTarget;

        public bool ValidForCheckedGrid =>
            X % 2 == 1 && Y % 2 == 0 || X % 2 == 0 && Y % 2 == 1;

        public bool ValidForTripleCheckedGrid =>
            X % 3 == 0 && Y % 3 == 2 ||
            X % 3 == 1 && Y % 3 == 0 ||
            X % 3 == 2 && Y % 3 == 1;


        public MapPoint(int row, int column, char point = '.')
        {
            X = row;
            Y = column;
            Type = (MapPointType)point;
        }

        public MapPoint()
        {
            Type = MapPointType.Outside;
        }
    }
}

