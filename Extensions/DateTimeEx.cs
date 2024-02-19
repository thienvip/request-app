using System;
using System.Collections.Generic;
using System.Linq;
namespace Asp.Web.Extensions
{
    public static class DateTimeEx
    {
        public static IEnumerable<DateTime[]> GetWeeks(this string start, string end)
        {
            DateTime tempStart;
            DateTime tempEnd;
            if (!string.IsNullOrWhiteSpace(start))
                throw new ArgumentNullException("start", "The start parameter cannot be null or empty");

            if (!string.IsNullOrWhiteSpace(end))
                throw new ArgumentNullException("end", "The end parameter cannot be null or empty");

            if (!DateTime.TryParse(start, out tempStart))
                throw new ArgumentException("Cannot parse the start parameter to a date time", "start");

            if (!DateTime.TryParse(end, out tempEnd))
                throw new ArgumentException("Cannot parse the end parameter to a date time", "end");

            return tempStart.GetWeeks(tempEnd);
        }

        public static IEnumerable<DateTime[]> GetWeeks(this string start, DateTime end)
        {
            DateTime tempStart;
            if (!string.IsNullOrWhiteSpace(start))
                throw new ArgumentNullException("start", "The start parameter cannot be null or empty");

            if (!DateTime.TryParse(start, out tempStart))
                throw new ArgumentException("Cannot parse the start parameter to a date time", "start");

            return tempStart.GetWeeks(end);
        }

        public static IEnumerable<DateTime[]> GetWeeks(this DateTime start, string end)
        {
            DateTime tempEnd;
            if (!string.IsNullOrWhiteSpace(end))
                throw new ArgumentNullException("end", "The end parameter cannot be null or empty");

            if (!DateTime.TryParse(end, out tempEnd))
                throw new ArgumentException("Cannot parse the end parameter to a date time", "end");

            return start.GetWeeks(tempEnd);
        }

        public static IEnumerable<DateTime[]> GetWeeks(this DateTime start, DateTime end)
        {
            return (from w in Enumerable.Range(0, (int)(end.AddDays(-(int)end.DayOfWeek).AddDays(7).Subtract(start.AddDays(-(int)start.DayOfWeek)).TotalDays / 7))
                    select new DateTime[] { start.AddDays(-(int)start.DayOfWeek).AddDays(w * 7), start.AddDays(-(int)start.DayOfWeek).AddDays((w * 7) + 7) });
        }


        public class WeekRange
        {
            public string Range { get; set; }
            public DateTime StartDate { get; set; }
            public DateTime EndDate { get; set; }
            public int Week { get; set; }
        }


        public static List<WeekRange> WeekDays(DateTime startDate, DateTime endDate)
        {
            DateTime startDateToCheck = startDate;
            DateTime dateToCheck = startDate;
            DateTime dateRangeBegin = dateToCheck;
            DateTime dateRangeEnd = endDate;

            List<WeekRange> weekRangeList = new List<WeekRange>();
            WeekRange weekRange = new WeekRange();


            while ((startDateToCheck.Year <= endDate.Year) && (startDateToCheck.Month <= endDate.Month) && dateToCheck <= endDate)
            {
                int week = 0;

                while (startDateToCheck.Month == dateToCheck.Month && dateToCheck <= endDate)
                {


                    week = week + 1;
                    dateRangeBegin = dateToCheck.AddDays(-(int)dateToCheck.DayOfWeek);
                    dateRangeEnd = dateToCheck.AddDays(6 - (int)dateToCheck.DayOfWeek);

                    if ((dateRangeBegin.Date < dateToCheck) && (dateRangeBegin.Date.Month != dateToCheck.Month))
                    {
                        dateRangeBegin = new DateTime(dateToCheck.Year, dateToCheck.Month, dateToCheck.Day);
                    }

                    if ((dateRangeEnd.Date > dateToCheck) && (dateRangeEnd.Date.Month != dateToCheck.Month))
                    {
                        DateTime dtTo = new DateTime(dateToCheck.Year, dateToCheck.Month, 1);
                        dtTo = dtTo.AddMonths(1);
                        dateRangeEnd = dtTo.AddDays(-(dtTo.Day));
                    }
                    if (dateRangeEnd.Date > endDate)
                    {
                        dateRangeEnd = new DateTime(dateRangeEnd.Year, dateRangeEnd.Month, endDate.Day);
                    }
                    weekRange = new WeekRange
                    {
                        StartDate = dateRangeBegin,
                        EndDate = dateRangeEnd,
                        Range = dateRangeBegin.Date.ToShortDateString() + '-' + dateRangeEnd.Date.ToShortDateString(),
                        Week = week
                    };
                    weekRangeList.Add(weekRange);
                    dateToCheck = dateRangeEnd.AddDays(1);
                    //startDateToCheck = startDateToCheck.AddMonths(1);
                }
            }

            return weekRangeList;
        }

        public static List<WeekRange> getWeekRange(DateTime startDate, DateTime endDate)
        {
            List<WeekRange> weekRangeList = new List<WeekRange>();
            WeekRange weekRange = new WeekRange();
            int week = 0;
            foreach (var t in Split(new DateTime(2021, 7, 5), new DateTime(2021, 8, 6), 4))
            {
                week = week + 1;
                weekRange = new WeekRange
                {
                    StartDate = t.Item1,
                    EndDate = t.Item2,
                    Range = t.Item1.Date.ToShortDateString() + '-' + t.Item2.ToShortDateString(),
                    Week = week
                };
                weekRangeList.Add(weekRange);
            }
            return weekRangeList;
        }
        public static IEnumerable<Tuple<DateTime, DateTime>> Split(DateTime start, DateTime end, int chunk)
        {
            DateTime chunkEnd;
            while ((chunkEnd = start.AddDays(chunk)) < end)
            {
                yield return Tuple.Create(start, chunkEnd);
                start = chunkEnd.AddDays(3);
            }
            yield return Tuple.Create(start, start.AddDays(chunk));
        }

    }
}
