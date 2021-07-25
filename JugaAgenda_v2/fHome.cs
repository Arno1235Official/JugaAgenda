﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.Calendar;

namespace JugaAgenda_v2
{

    public partial class fHome : Form
    {

        GoogleCalendar googleCalendar;

        public fHome()
        {
            InitializeComponent();

            googleCalendar = new GoogleCalendar();
            googleCalendar.refreshEvents();

            mvHome.SelectionStart = DateTime.Now.StartOfWeek(DayOfWeek.Monday).Date;
            mvHome.SelectionEnd = DateTime.Now.EndOfWeek(DayOfWeek.Sunday);
            mvHome.MaxSelectionCount = calHome.MaximumViewDays;

            calHome.TimeScale = CalendarTimeScale.SixtyMinutes;

        }

        private void mvHome_SelectionChanged(object sender, EventArgs e)
        {
            if ((mvHome.SelectionEnd - mvHome.SelectionStart).Days > -1 && (mvHome.SelectionEnd - mvHome.SelectionStart).Days < calHome.MaximumViewDays)
            {
                int oldMaximumViewDays = calHome.MaximumViewDays;
                DateTime end;
                DateTime start;
                if (mvHome.SelectionEnd > calHome.ViewEnd)
                {
                    end = mvHome.SelectionEnd;
                } else
                {
                    end = calHome.ViewEnd;
                }
                if (mvHome.SelectionStart < calHome.ViewStart)
                {
                    start = mvHome.SelectionStart;
                }
                else
                {
                    start = calHome.ViewStart;
                }
                calHome.MaximumViewDays = (int)Math.Ceiling((double)(end - start).Days / 7) * 7 + 7;
                if (mvHome.SelectionStart <= calHome.ViewEnd)
                {
                    calHome.ViewStart = mvHome.SelectionStart;
                    calHome.ViewEnd = mvHome.SelectionEnd;
                }
                else
                {
                    calHome.ViewEnd = mvHome.SelectionEnd;
                    calHome.ViewStart = mvHome.SelectionStart;
                }
                calHome.MaximumViewDays = oldMaximumViewDays;

                foreach (var eventItem in googleCalendar.getEvents().Items)
                {
                    if (eventItem.Start.DateTime != null)
                    {
                        if ((eventItem.Start.DateTime > calHome.ViewStart && eventItem.Start.DateTime < calHome.ViewEnd) ||
                            (eventItem.End.DateTime < calHome.ViewEnd && eventItem.End.DateTime > calHome.ViewStart))
                        {
                            CalendarItem newItem = new CalendarItem(calHome,
                                (DateTime)eventItem.Start.DateTime,
                                (DateTime)eventItem.End.DateTime,
                                eventItem.Summary);

                            calHome.Items.Add(newItem);
                        }
                    } else
                    {
                        DateTime startDate = Convert.ToDateTime(eventItem.Start.Date);
                        DateTime endDate = Convert.ToDateTime(eventItem.End.Date);
                        if ((startDate > calHome.ViewStart && startDate < calHome.ViewEnd) ||
                            (endDate < calHome.ViewEnd && endDate > calHome.ViewStart))
                        {
                            CalendarItem newItem = new CalendarItem(calHome,
                                startDate,
                                endDate.AddSeconds(-1),
                                eventItem.Summary);

                            calHome.Items.Add(newItem);
                        }
                    }
                }

            } else
            {
                MessageBox.Show("The selection has to be at least 1 day and can't be more than " + calHome.MaximumViewDays.ToString() + " days.");
            }
            

        }

        private void calHome_ItemDoubleClick(object sender, EventArgs e)
        {
            MessageBox.Show(calHome.GetSelectedItems().ToList()[0].Text.ToString());
        }

        #region SimpleButtonFunctions
        private void manualToolStripMenuItem_Click(object sender, EventArgs e)
        {
            mvHome.SelectionMode = System.Windows.Forms.Calendar.MonthView.MonthViewSelection.Manual;
        }

        private void dayToolStripMenuItem_Click(object sender, EventArgs e)
        {
            mvHome.SelectionMode = System.Windows.Forms.Calendar.MonthView.MonthViewSelection.Day;
        }

        private void workweekToolStripMenuItem_Click(object sender, EventArgs e)
        {
            mvHome.SelectionMode = System.Windows.Forms.Calendar.MonthView.MonthViewSelection.WorkWeek;
        }

        private void weekToolStripMenuItem_Click(object sender, EventArgs e)
        {
            mvHome.SelectionMode = System.Windows.Forms.Calendar.MonthView.MonthViewSelection.Week;
        }

        private void monthToolStripMenuItem_Click(object sender, EventArgs e)
        {
            mvHome.SelectionMode = System.Windows.Forms.Calendar.MonthView.MonthViewSelection.Month;
        }

        private void fiveMinutesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            calHome.TimeScale = System.Windows.Forms.Calendar.CalendarTimeScale.FiveMinutes;
        }

        private void sixMinutesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            calHome.TimeScale = System.Windows.Forms.Calendar.CalendarTimeScale.SixMinutes;
        }

        private void tenMinutesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            calHome.TimeScale = System.Windows.Forms.Calendar.CalendarTimeScale.TenMinutes;
        }

        private void fifteenMinutesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            calHome.TimeScale = System.Windows.Forms.Calendar.CalendarTimeScale.FifteenMinutes;
        }

        private void thirtyMinutesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            calHome.TimeScale = System.Windows.Forms.Calendar.CalendarTimeScale.ThirtyMinutes;
        }

        private void sixtyMinutesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            calHome.TimeScale = System.Windows.Forms.Calendar.CalendarTimeScale.SixtyMinutes;
        }

        private void btTestGoogleConnection_Click(object sender, EventArgs e)
        {
            googleCalendar.refreshEvents();
        }
        private void oneMonthToolStripMenuItem_Click(object sender, EventArgs e)
        {
            googleCalendar.setPerspectiveMonths(1);
        }
        private void twoMonthsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            googleCalendar.setPerspectiveMonths(2);
        }
        private void threeMonthsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            googleCalendar.setPerspectiveMonths(3);
        }
        private void fourMonthsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            googleCalendar.setPerspectiveMonths(4);
        }
        private void sixMonthsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            googleCalendar.setPerspectiveMonths(6);
        }

        #endregion

    }

    public static class DateTimeExtensions
    {
        public static DateTime StartOfWeek(this DateTime dt, DayOfWeek startOfWeek)
        {
            int diff = (7 + (dt.DayOfWeek - startOfWeek)) % 7;
            return dt.AddDays(-1 * diff).Date;
        }

        public static DateTime EndOfWeek(this DateTime dt, DayOfWeek endOfWeek)
        {
            int diff = (7 - (dt.DayOfWeek - endOfWeek)) % 7;
            return dt.AddDays(diff).Date;
        }
    }

}
