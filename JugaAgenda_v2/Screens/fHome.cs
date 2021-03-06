using JugaAgenda_v2.Classes;
using JugaAgenda_v2.Screens;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Forms.Calendar;
using System.Text.RegularExpressions;

namespace JugaAgenda_v2
{

    public partial class fHome : Form
    {

        public GoogleCalendar googleCalendar;
        private List<CustomDay> techniciansWorkWeekList;
        private List<CustomDay> technicianLeaveList;
        private List<CustomDay> workList;
        private List<Tuple<DateTime, String, decimal>> openWorkHoursList;
        private List<Technician> technicianList;
        private fCalendarEvent calendarEventScreen = null;
        private fSearch searchScreen = null;
        private fPlanning planningScreen = null;
        private Google.Apis.Calendar.v3.Data.Event wrongTitleSelected = null;

        #region TODO

        // - Jurgen screen 3
        // - Add extra support in calendar screen
        // - Add/Edit/Remove/Show tech leave
        // - Add/Edit/Remove/Show tech schedule
        // - Add list of basic work with duration and price
        // - Translate

        #endregion

        #region InitializationFunctions

        public fHome()
        {

            InitializeComponent();

            googleCalendar = new GoogleCalendar();

            mvHome.SelectionStart = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            mvHome.SelectionEnd = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.DaysInMonth(DateTime.Now.Year, DateTime.Now.Month));
            mvHome.MaxSelectionCount = calHome.MaximumViewDays;

            mvHome.SelectionChanged += new System.EventHandler(mvHome_SelectionChanged);

            calHome.TimeScale = CalendarTimeScale.SixtyMinutes;

            DateTime now = DateTime.Now;
            newDayTimer.Interval = 1000 - now.Millisecond + (59 - now.Second)*1000 + (59 - now.Minute)*1000*60 + (23 - now.Hour)*1000*60*60 + 1*60*1000;
            newDayTimer.Enabled = true;

            loadStyleComponents();

            loadEverything();

        }

        public void loadEverything()
        {
            refresh();

            mvHome_SelectionChanged(null, null);

            refreshTimer.Enabled = true;

        }

        private void loadStyleComponents()
        {

            cbCalendarSelectionMode.Items.Add("Manueel");
            cbCalendarSelectionMode.Items.Add("Dag");
            cbCalendarSelectionMode.Items.Add("Werk Week");
            cbCalendarSelectionMode.Items.Add("Week");
            cbCalendarSelectionMode.Items.Add("Maand");
            cbCalendarSelectionMode.SelectedIndex = 4;
            cbCalendarSelectionMode.DropDownStyle = ComboBoxStyle.DropDownList;

            cbCalendarTimeScale.Items.Add("5 minuten");
            cbCalendarTimeScale.Items.Add("6 minuten");
            cbCalendarTimeScale.Items.Add("10 minuten");
            cbCalendarTimeScale.Items.Add("15 minuten");
            cbCalendarTimeScale.Items.Add("30 minuten");
            cbCalendarTimeScale.Items.Add("60 minuten");
            cbCalendarTimeScale.SelectedIndex = 5;
            cbCalendarTimeScale.DropDownStyle = ComboBoxStyle.DropDownList;

            cbCalendarPerspective.Items.Add("1 maand");
            cbCalendarPerspective.Items.Add("2 maanden");
            cbCalendarPerspective.Items.Add("3 maanden");
            cbCalendarPerspective.Items.Add("4 maanden");
            cbCalendarPerspective.Items.Add("5 maanden");
            cbCalendarPerspective.SelectedIndex = 0;
            cbCalendarPerspective.DropDownStyle = ComboBoxStyle.DropDownList;

            foreach (Work.Status status in Enum.GetValues(typeof(Work.Status))) cbStatus.Items.Add(status);
            cbStatus.SelectedIndex = 0;

            nuHoursDone.Maximum = 0;

        }

        #endregion

        #region CalendarSynchronizationFunctions

        public void refresh()
        {
            testConnection();
            loadTechniciansWorkWeek();
            loadTechnicianLeave();
            loadWork();
        }

        private void refreshTimer_Tick(object sender, EventArgs e)
        {
            
            try
            {
                syncCalendar();
            } catch {
                MessageBox.Show("Er is iets fout gelopen tijdens het synchroniseren van de agenda.");
            }

        }

        // Can be more efficient
        public void syncCalendar()
        {
            IList<Google.Apis.Calendar.v3.Data.Event> syncList = googleCalendar.sync();
            if (syncList != null)
            {
                foreach (Google.Apis.Calendar.v3.Data.Event item in syncList)
                {
                    bool found = false;
                    foreach (CustomDay day in workList)
                    {
                        List<Work> dayWorkList = day.getWorkList();
                        for (int i = dayWorkList.Count - 1; i >= 0; i--)
                        {
                            Work work = dayWorkList[i];

                            if (work.getId() == item.Id)
                            {

                                IEnumerable<Tuple<DateTime, String, decimal>> openWorkHoursToDelete = openWorkHoursList.Where(x => x.Item2.Equals(item.Id));
                                if (openWorkHoursToDelete.Count() > 1)
                                {
                                    foreach (Tuple<DateTime, String, decimal> tuple in openWorkHoursToDelete)
                                    {
                                        openWorkHoursList.Remove(tuple);
                                    }
                                }
                                else if (openWorkHoursToDelete.Count() == 1)
                                {
                                    openWorkHoursList.Remove(openWorkHoursToDelete.First());
                                }

                                if (item.Summary != null && checkTitleMessageBox(item, false))
                                {
                                    work.updateValues(item);

                                    if (day.getDate() < DateTime.Now.StartOfWeek(DayOfWeek.Monday) &&
                                        work.isWorkOpen())
                                    {
                                        openWorkHoursList.Add(new Tuple<DateTime, string, decimal>(day.getDate(), work.getId(), work.getDuration() - work.getHoursDone()));
                                    }

                                }
                                else
                                {
                                    day.getWorkList().RemoveAt(i);
                                }
                                found = true;
                                break;
                            }

                        }
                        if (found) break;
                    }

                    if (!found)
                    {
                        removeWrongTitles(item.Id);
                        
                        if (item.Summary != null) addWorkItem(item);
                    }

                }
                // Can be more efficient
                mvHome_SelectionChanged(null, null);
            }
        }

        #endregion

        #region LoadCalendarFunctions

        // Can be more efficient
        private void loadTechniciansWorkWeek()
        {
            if (techniciansWorkWeekList == null) techniciansWorkWeekList = new List<CustomDay>();
            techniciansWorkWeekList.Clear();

            if (technicianList == null) technicianList = new List<Technician>();
            technicianList.Clear();

            for (int i = 1; i <= 7; i++) techniciansWorkWeekList.Add(new CustomDay(new DateTime(2021, 2, i)));

            foreach (Google.Apis.Calendar.v3.Data.Event item in googleCalendar.getTechnicianEvents().Items)
            {
                DateTime date = Convert.ToDateTime(item.Start.Date);
                if (date.Day > 7) continue;

                /*Regex regex = new Regex("[a-zA-Z ]+ [0-9,.]+u", RegexOptions.IgnoreCase);
                if (!regex.IsMatch(item.Summary))
                {
                    MessageBox.Show("There seems to be something wrong in the technician schedule.", "Error");
                    continue;
                }*/

                if (checkTitleWithRegex(item, "^[a-zA-Z ]+ [0-9,.]+u$", true))
                {
                    CustomDay day = techniciansWorkWeekList[date.Day-1];

                    String name = item.Summary.Remove(item.Summary.Length - item.Summary.Split(' ').Last().Length - 1);
                    decimal hours = Convert.ToDecimal(item.Summary.Split(' ').Last().Split('u')[0].Replace(',', '.'));
                    day.addTechnicianList(new Technician(name, hours));

                    Technician technician = new Technician(name);
                    if (!technicianList.Contains(technician)) technicianList.Add(technician);
                }


            }

            /*foreach (CustomDay day in techniciansWorkWeekList)
            {
                foreach (Technician tech in day.getTechnicianList())
                {
                    MessageBox.Show(day.getDate().customToString() + " " + tech.getName() + " " + tech.getHours().ToString());
                }
            }*/
        }

        private void loadTechnicianLeave()
        {
            if (technicianLeaveList == null) technicianLeaveList = new List<CustomDay>();
            technicianLeaveList.Clear();

            foreach (Google.Apis.Calendar.v3.Data.Event item in googleCalendar.getLeaveEvents().Items)
            {

                /*Regex regex = new Regex("[a-zA-Z ]+ verlof", RegexOptions.IgnoreCase);
                if (!regex.IsMatch(item.Summary))
                {
                    MessageBox.Show("There seems to be something wrong in the technician leave schedule.", "Error");
                    continue;
                }*/

                if (checkTitleWithRegex(item, "^[a-zA-Z ]+ verlof$", false))
                {

                    DateTime date = Convert.ToDateTime(item.Start.Date);

                    CustomDay day = null;

                    foreach (CustomDay listday in technicianLeaveList)
                    {
                        if (DateTime.Compare(listday.getDate(), date) == 0)
                        {
                            day = listday;
                            break;
                        }
                    }
                    if (day == null)
                    {
                        day = new CustomDay(date);

                        technicianLeaveList.Add(day);
                    }

                    day.addTechnicianList(new Technician(item.Summary.Remove(item.Summary.Length-7), 0));
                }

            }

            /*foreach (CustomDay day in technicianLeaveList)
            {
                foreach (Technician tech in day.getTechnicianList())
                {
                    MessageBox.Show(day.getDate().ToString() + " " + tech.getName() + " " + tech.getHours().ToString());
                }
            }*/
        }

        // No support for non all-day and multiple days events yet
        private void loadWork()
        {
            if (workList == null) workList = new List<CustomDay>();
            workList.Clear();

            if (openWorkHoursList == null) openWorkHoursList = new List<Tuple<DateTime, String, decimal>>();
            openWorkHoursList.Clear();

            foreach (Google.Apis.Calendar.v3.Data.Event item in googleCalendar.getWorkEvents())
            {
                addWorkItem(item);
            }

            MessageBox.Show("There are " + nuWrongTitles.Value.ToString() + " titles wrong.");

            /*foreach (CustomDay day in workList)
            {
                MessageBox.Show(day.getDate().ToString(), "Date");
                foreach (Work work in day.getWorkList())
                {
                    MessageBox.Show(work.getClientName() + " " + work.getDescription() + " " + work.getDuration().ToString() + " " + work.getOrderNumber() + " " + work.getPhoneNumber() + " " + work.getStatus().ToString());
                    *//*MessageBox.Show(work.getClientName(), "Client name");
                    MessageBox.Show(work.getDescription(), "Description");
                    MessageBox.Show(work.getDuration().ToString(), "Duration");
                    MessageBox.Show(work.getOrderNumber(), "Order number");
                    MessageBox.Show(work.getPhoneNumber(), "Phone number");
                    MessageBox.Show(work.getStatus().ToString(), "Status");*//*
                }
                foreach (Technician tech in day.getTechnicianList())
                {
                    MessageBox.Show(day.getDate().ToString() + " " + tech.getName() + " " + tech.getHours().ToString());
                }
            }*/
        }

        // Work.Status.onderdelen_niet_op_tijd ook voor availability?
        // Can be more efficient
        private void addWorkItem(Google.Apis.Calendar.v3.Data.Event item)
        {

            DateTime date;
            if (item.Start.DateTime != null)
            {
                date = (DateTime)item.Start.DateTime;
                date = new DateTime(date.Year, date.Month, date.Day, 0, 0, 0, 0);
            }
            else
            {
                date = Convert.ToDateTime(item.Start.Date);
            }

            CustomDay day = null;
            foreach (CustomDay listday in workList)
            {
                if (DateTime.Compare(listday.getDate(), date) == 0)
                {
                    day = listday;
                    break;
                }
            }
            if (day == null)
            {
                day = new CustomDay(date);

                foreach (Technician tech in techniciansWorkWeekList[(int)date.DayOfWeek].getTechnicianList())
                {
                    bool tech_has_leave = false;
                    foreach (CustomDay leave_day in technicianLeaveList)
                    {
                        if (DateTime.Compare(day.getDate(), leave_day.getDate()) == 0)
                        {
                            foreach (Technician leave_tech in leave_day.getTechnicianList())
                            {
                                if (tech.getName().Equals(leave_tech.getName())) tech_has_leave = true;
                            }
                            // if (!tech_has_leave) MessageBox.Show("There seems to be a wrong name or date in the technician leave schedule", "Error");
                        }
                    }

                    if (!tech_has_leave) day.addTechnicianList(tech);
                }

                workList.Add(day);
            }

            if (checkTitleMessageBox(item, false))
            {
                Work new_work = new Work(item);
                day.addWorkList(new_work);
                if (date < DateTime.Now.StartOfWeek(DayOfWeek.Monday) &&
                    new_work.isWorkOpen())
                        openWorkHoursList.Add(new Tuple<DateTime, string, decimal>(date, new_work.getId(), new_work.getDuration() - new_work.getHoursDone()));
            }
            else
            {
                addWrongTitles(item);
            }

        }

        #endregion

        // TODO
        #region ExtraPrimaryFunctions

        // TODO: Multiple days and not full days events
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
                }
                else
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
                    if (mvHome.SelectionMode == System.Windows.Forms.Calendar.MonthView.MonthViewSelection.Month)
                    {
                        calHome.ViewStart = mvHome.SelectionStart.StartOfWeek(DayOfWeek.Monday);
                        calHome.ViewEnd = mvHome.SelectionEnd.EndOfWeek(DayOfWeek.Sunday);
                    } else
                    {
                        calHome.ViewStart = mvHome.SelectionStart;
                        calHome.ViewEnd = mvHome.SelectionEnd;
                    }

                }
                else
                {
                    if (mvHome.SelectionMode == System.Windows.Forms.Calendar.MonthView.MonthViewSelection.Month)
                    {
                        calHome.ViewEnd = mvHome.SelectionEnd.EndOfWeek(DayOfWeek.Sunday);
                        calHome.ViewStart = mvHome.SelectionStart.StartOfWeek(DayOfWeek.Monday);
                    }
                    else
                    {
                        calHome.ViewEnd = mvHome.SelectionEnd;
                        calHome.ViewStart = mvHome.SelectionStart;
                    }
                }
                calHome.MaximumViewDays = oldMaximumViewDays;

                if (workList != null)
                {
                    foreach (CustomDay day in workList)
                    {
                        DateTime date = day.getDate();

                        if (date >= calHome.ViewStart && date < calHome.ViewEnd)
                        {
                            foreach (Work work in day.getWorkList())
                            {
                                CalendarItem newItem = new CalendarItem(calHome,
                                    date,
                                    date.AddDays(1).AddSeconds(-1),
                                    work.getTitle());

                                newItem.ApplyColor(work.getColor());
                                newItem.setCalendarEvent(work.getCalendarEvent());

                                calHome.Items.Add(newItem);
                            }
                        }
                    }
                }


                /*foreach (Google.Apis.Calendar.v3.Data.Event eventItem in googleCalendar.getWorkEvents().Items)
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
                    }
                    else
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
                }*/

            }
            else
            {
                MessageBox.Show("The selection has to be at least 1 day and can't be more than " + calHome.MaximumViewDays.ToString() + " days.");
            }


        }

        public bool deleteWorkItem(String eventId)
        {
            return googleCalendar.deleteWorkEvent(eventId);
        }

        public List<Work> searchWork(String clientName, String phoneNumber, String orderNumber, String description, Boolean OR = true)
        {
            List<Work> results = new List<Work>();

            foreach (CustomDay day in workList)
            {
                foreach (Work work in day.getWorkList())
                {
                    if (OR)
                    {
                        if (
                            (clientName != null && clientName != "" && work.getClientName() != null && work.getClientName().Contains(clientName, StringComparison.OrdinalIgnoreCase)) ||
                            (phoneNumber != null && phoneNumber != "" && work.getPhoneNumber() != null && work.getPhoneNumber().Contains(phoneNumber, StringComparison.OrdinalIgnoreCase)) ||
                            (orderNumber != null && orderNumber != "" && work.getOrderNumber() != null && work.getOrderNumber().Contains(orderNumber, StringComparison.OrdinalIgnoreCase)) ||
                            (description != null && description != "" && work.getDescription() != null && work.getDescription().Contains(description, StringComparison.OrdinalIgnoreCase))
                            )
                        {
                            results.Add(work);
                        }
                    }
                    else
                    {
                        if (
                            (clientName == null || clientName == "" || work.getClientName() == null || work.getClientName().Contains(clientName, StringComparison.OrdinalIgnoreCase)) &&
                            (phoneNumber == null || phoneNumber == "" || work.getPhoneNumber() == null || work.getPhoneNumber().Contains(phoneNumber, StringComparison.OrdinalIgnoreCase)) &&
                            (orderNumber == null || orderNumber == "" || work.getOrderNumber() == null || work.getOrderNumber().Contains(orderNumber, StringComparison.OrdinalIgnoreCase)) &&
                            (description == null || description == "" || work.getDescription() == null || work.getDescription().Contains(description, StringComparison.OrdinalIgnoreCase)) &&
                            !(
                            (clientName == null || clientName == "" || work.getClientName() == null) &&
                            (phoneNumber == null || phoneNumber == "" || work.getPhoneNumber() == null) &&
                            (orderNumber == null || orderNumber == "" || work.getOrderNumber() == null) &&
                            (description == null || description == "" || work.getDescription() == null)
                            )
                            )
                        {
                            results.Add(work);
                        }
                    }
                }
            }

            return results;
        }

        public List<DateTime> checkAvailability(decimal duration)
        {
            List<DateTime> results = new List<DateTime>();

            DateTime date = DateTime.Today.StartOfWeek(DayOfWeek.Monday);
            decimal hoursTally = 0;

            openWorkHoursList.Sort((x, y) => y.Item1.CompareTo(x.Item1));
            foreach (Tuple<DateTime, String, decimal> item in openWorkHoursList)
            {
                if (item.Item1 >= date)
                {
                    break;
                }
                hoursTally -= item.Item3;
            }

            while(results.Count < 3)
            {
                hoursTally += techHoursAvailable(date);
                hoursTally -= workHoursOnDay(date);

                if (hoursTally >= duration) results.Add(date);

                date = date.AddDays(1);
            }

            return results;
        }

        private decimal techHoursAvailable(DateTime date)
        {
            decimal hoursTally = 0;

            IEnumerable<CustomDay> workDays = techniciansWorkWeekList.Where(x => x.getDate().DayOfWeek.Equals(date.DayOfWeek));
            if (workDays.Count() > 0)
            {
                List<Technician> techs = workDays.First().getTechnicianList();
                foreach (Technician tech in techs)
                {
                    hoursTally += tech.getHours();
                }
            }

            IEnumerable<CustomDay> leaveDays = technicianLeaveList.Where(x => x.getDate().Equals(date));
            if (leaveDays.Count() > 0)
            {
                List<Technician> techLeaves = leaveDays.First().getTechnicianList();
                foreach (Technician tech in techLeaves)
                {
                    hoursTally -= tech.getHours();
                }
            }

            return hoursTally;
        }

        // TODO: check if work isn't already done
        private decimal workHoursOnDay(DateTime date)
        {
            decimal hoursTally = 0;

            IEnumerable<CustomDay> days = workList.Where(x => x.getDate().Equals(date));
            if (days.Count() > 0)
            {
                List<Work> workHours = days.First().getWorkList();
                foreach (Work work in workHours)
                {
                    hoursTally += work.getDuration() - work.getHoursDone();
                }
            }
            
            return hoursTally;
        }

        public List<Work> getWorkWithNoAvailableComponents(DateTime start, DateTime end)
        {
            List<Work> results = new List<Work>();

            for (DateTime date = start; date <= end; date = date.AddDays(1))
            {
                IEnumerable<CustomDay> workDays = workList.Where(x => x.getDate().Year.Equals(date.Year) && x.getDate().Month.Equals(date.Month) && x.getDate().Day.Equals(date.Day));

                if (workDays.Count() > 0)
                {
                    List<Work> works = workDays.First().getWorkList();
                    foreach (Work work in works)
                    {
                        if (!work.isWorkReady())
                        {
                            results.Add(work);
                        }
                    }
                }
            }

            return results;
        }

        public List<Work> getWorkNotFinished()
        {
            List<Work> results = new List<Work>();

            foreach (Tuple<DateTime, String, decimal> work in openWorkHoursList)
            {
                IEnumerable<CustomDay> workDayList = workList.Where(x => x.getDate().Equals(work.Item1));
                if (workDayList.Count() > 0)
                {
                    IEnumerable<Work> works = workDayList.First().getWorkList().Where(x => x.getId().Equals(work.Item2));
                    if (works.Count() > 0)
                        results.Add(works.First());
                }
            }

            return results;
        }

        public List<CustomDay> getWorkBetweenDates(DateTime startDate, DateTime endDate)
        {
            List<CustomDay> results = new List<CustomDay>();

            foreach (Tuple<DateTime, String, decimal> work in openWorkHoursList)
            {

                CustomDay customDay = new CustomDay(work.Item1);
                if (results.Contains(customDay))
                {
                    customDay = results.Where(x => x.getDate().Year.Equals(work.Item1.Year) && x.getDate().Month.Equals(work.Item1.Month) && x.getDate().Day.Equals(work.Item1.Day)).First();
                }
                else
                {
                    results.Add(customDay);
                }

                IEnumerable<CustomDay> workDayList = workList.Where(x => x.getDate().Equals(work.Item1));
                if (workDayList.Count() > 0)
                {
                    IEnumerable<Work> works = workDayList.First().getWorkList().Where(x => x.getId().Equals(work.Item2));
                    if (works.Count() > 0)
                        customDay.addWorkList(works.First());
                }

            }

            for (DateTime date = startDate; date <= endDate; date.AddDays(1))
            {
                IEnumerable<CustomDay> days = workList.Where(x => x.getDate().Year.Equals(date.Year) && x.getDate().Month.Equals(date.Month) && x.getDate().Day.Equals(date.Day));
                if (days.Count() > 0)
                    results.Add(days.First());
            }

            return results;
        }

        #endregion

        // TODO
        #region ExtraSecondaryFunctions

        // TODO: Change this to be completely implemented in work class
        private bool checkTitleMessageBox(Google.Apis.Calendar.v3.Data.Event item, Boolean askForTitleChange)
        {
            if (!askForTitleChange) return new Work().check_title(item.Summary);
            if (!new Work().check_title(item.Summary))
            {
                while (true)
                {
                    String new_title = Microsoft.VisualBasic.Interaction.InputBox("Please change the title of this event.", "Wrong event title", item.Summary);
                    if (!new_title.Equals("") && new_title != null)
                    {
                        if (!new Work().check_title(new_title))
                        {
                            MessageBox.Show("The new title is still incorrect.");
                            continue;
                        }
                        item.Summary = new_title;
                        if (!googleCalendar.editWorkEvent(item))
                        {
                            MessageBox.Show("Something went wrong when updating event to calendar.");
                            return false;
                        }
                        return true;
                    }
                    else
                    {
                        //MessageBox.Show("Please change the title.");
                        return false;
                    }
                }
            }
            else
            {
                return true;
            }
        }

        private bool checkTitleWithRegex(Google.Apis.Calendar.v3.Data.Event item, String regexString, Boolean trueForScheduleFalseForLeave)
        {
            Regex regex = new Regex(regexString, RegexOptions.IgnoreCase);
            if (!regex.IsMatch(item.Summary))
            {
                while (true)
                {
                    String new_title;

                    if (trueForScheduleFalseForLeave)
                        new_title = Microsoft.VisualBasic.Interaction.InputBox("Please change the title.", "Wrong schedule event title", item.Summary);
                    else
                        new_title = Microsoft.VisualBasic.Interaction.InputBox("Please change the title.", "Wrong leave event title", item.Summary);

                    if (!new_title.Equals("") && new_title != null)
                    {
                        if (!regex.IsMatch(new_title))
                        {
                            MessageBox.Show("The new title is still incorrect.");
                            continue;
                        }
                        item.Summary = new_title;
                        if (trueForScheduleFalseForLeave)
                        {
                            if (!googleCalendar.editTechnicianEvent(item))
                            {
                                MessageBox.Show("Something went wrong when updating event to calendar.");
                                return false;
                            }
                            else return true;
                        }
                        else if (!trueForScheduleFalseForLeave)
                        {
                            if (!googleCalendar.editLeaveEvent(item))
                            {
                                MessageBox.Show("Something went wrong when updating event to calendar.");
                                return false;
                            }
                            else return true;
                        }

                        //return true;
                    }
                    else
                    {
                        //MessageBox.Show("Please change the title.");
                        return false;
                    }
                }
            }
            else
            {
                return true;
            }
        }

        private void testConnection(bool print_if_successfull = false)
        {
            if (googleCalendar.testConnection())
            {
                if (print_if_successfull) MessageBox.Show("Google Calendar Connection Succesfull");
            }
            else
            {
                MessageBox.Show("Google Calendar Connection Error");
                this.Close();
            }
        }

        // Can be more efficient
        public Google.Apis.Calendar.v3.Data.Event getGoogleEventById(String id)
        {

            foreach (Google.Apis.Calendar.v3.Data.Event item in googleCalendar.getWorkEvents())
            {
                if (item.Id.Equals(id)) return item;
            }
            return null;
        }

        private void newDayTimer_Tick(object sender, EventArgs e)
        {
            loadEverything();
        }

        #endregion

        #region SecondaryScreensFunctions

        public void clear_calendar_screen()
        {
            calendarEventScreen = null;
        }

        public void clear_search_screen()
        {
            searchScreen = null;
        }

        public Boolean getCalendarScreenAlreadyOpen()
        {
            return this.calendarEventScreen != null;
        }

        public void openCalendarScreen(Google.Apis.Calendar.v3.Data.Event item, fPlanning planningScreen = null)
        {
            if (calendarEventScreen == null)
            {
                calendarEventScreen = new fCalendarEvent(this, item, planningScreen);
                calendarEventScreen.Show();
            }
        }

        private void openSearchScreen()
        {
            if (searchScreen == null)
            {
                searchScreen = new fSearch(this);
                searchScreen.Show();
            }
        }

        public void clearPlanningScreen()
        {
            planningScreen = null;
        }

        public void lbWrongTitles_SizeChanged()
        {
            nuWrongTitles.Value = lbWrongTitles.Items.Count;
        }

        private void fHome_Resize(object sender, EventArgs e)
        {
            lbWrongTitles.Height = this.Height - 200;
            lbWrongTitles.Width = this.Width - gbWrongTitlesControl.Width - 75;

            System.Drawing.Point newLoc = new System.Drawing.Point();
            newLoc.X = this.Width - gbWrongTitlesControl.Width - 50;
            newLoc.Y = gbWrongTitlesControl.Location.Y;
            gbWrongTitlesControl.Location = newLoc;
        }

        private void updateNUTechHours()
        {
            nuHoursDone.Maximum = nuHours.Value;
        }

        private void btDelete_Click(object sender, EventArgs e)
        {
            if (wrongTitleSelected == null) return;

            DialogResult answer = MessageBox.Show("Weet je zeker dat je het agenda item wilt verwijderen?", "Opgelet", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

            if (answer == DialogResult.Yes)
            {
                if (deleteWorkItem(wrongTitleSelected.Id))
                {
                    clearWrongTitlesControl();
                }
                else
                {
                    MessageBox.Show("Er is iets fout gelopen.");
                }
            }
        }

        private void btSave_Click(object sender, EventArgs e)
        {
            if (wrongTitleSelected == null) return;

            Work work = new Work();
            work.updateValues(wrongTitleSelected, false);

            work.setStatus((Work.Status)cbStatus.SelectedItem);
            work.setDuration(nuHours.Value);
            work.setClientName(tbClientName.Text);
            work.setPhoneNumber(tbPhoneNumber.Text);
            work.setOrderNumber(tbOrderNumber.Text);
            work.setDescription(rtbDescription.Text);
            work.setHoursDone(nuHoursDone.Value);

            if (!work.checkValues()) return;

            work.updateCalendarEvent();

            if (googleCalendar.editWorkEvent(work.getCalendarEvent()))
            {
                clearWrongTitlesControl();
                syncCalendar();
            }
            else
            {
                MessageBox.Show("Something went wrong when updating the event to the calendar.");
            }
        }

        public void addWrongTitles(Google.Apis.Calendar.v3.Data.Event item)
        {
            lbWrongTitles.Items.Add(new WrongTitleObject(item));
            lbWrongTitles_SizeChanged();
        }

        public void removeWrongTitles(String id)
        {
            foreach (WrongTitleObject wrongTitleObject in lbWrongTitles.Items)
            {
                if (wrongTitleObject.getId().Equals(id))
                {
                    lbWrongTitles.Items.Remove(wrongTitleObject);
                    lbWrongTitles_SizeChanged();
                    return;
                }
            }
        }

        public void loadWrongTitlesControl()
        {
            WrongTitleObject wrongTitleObject = (WrongTitleObject)lbWrongTitles.SelectedItem;

            if (wrongTitleObject == null) return;

            wrongTitleSelected = wrongTitleObject.getEvent();

            tbOldTitle.Text = wrongTitleObject.getEvent().Summary;

            Work work = new Work();
            work.updateValues(wrongTitleObject.getEvent(), false);

            cbStatus.SelectedItem = work.getStatus();
            nuHours.Value = work.getDuration();
            tbClientName.Text = work.getClientName();
            tbPhoneNumber.Text = work.getPhoneNumber();
            tbOrderNumber.Text = work.getOrderNumber();
            rtbDescription.Text = work.getDescription();
            nuHoursDone.Maximum = nuHours.Value;
            nuHoursDone.Value = work.getHoursDone();
        }

        public void clearWrongTitlesControl()
        {

            wrongTitleSelected = null;

            tbOldTitle.Text = null;
            cbStatus.SelectedItem = null;
            nuHours.Value = 0;
            tbClientName.Text = null;
            tbPhoneNumber.Text = null;
            tbOrderNumber.Text = null;
            rtbDescription.Text = null;
            nuHoursDone.Maximum = 0;
            nuHoursDone.Value = 0;
        }

        #endregion

        #region SimpleButtonFunctions

        private void cbCalendarSelectionMode_SelectedIndexChanged(object sender, EventArgs e)
        {
            switch (cbCalendarSelectionMode.SelectedIndex)
            {
                case 0:
                    mvHome.SelectionMode = System.Windows.Forms.Calendar.MonthView.MonthViewSelection.Manual;
                    break;
                case 1:
                    mvHome.SelectionMode = System.Windows.Forms.Calendar.MonthView.MonthViewSelection.Day;
                    break;
                case 2:
                    mvHome.SelectionMode = System.Windows.Forms.Calendar.MonthView.MonthViewSelection.WorkWeek;
                    break;
                case 3:
                    mvHome.SelectionMode = System.Windows.Forms.Calendar.MonthView.MonthViewSelection.Week;
                    break;
                case 4:
                    mvHome.SelectionMode = System.Windows.Forms.Calendar.MonthView.MonthViewSelection.Month;
                    break;
                default:
                    mvHome.SelectionMode = System.Windows.Forms.Calendar.MonthView.MonthViewSelection.Month;
                    break;
            }
        }

        private void cbCalendarTimeScale_SelectedIndexChanged(object sender, EventArgs e)
        {
            switch (cbCalendarTimeScale.SelectedIndex)
            {
                case 0:
                    calHome.TimeScale = System.Windows.Forms.Calendar.CalendarTimeScale.FiveMinutes;
                    break;
                case 1:
                    calHome.TimeScale = System.Windows.Forms.Calendar.CalendarTimeScale.SixMinutes;
                    break;
                case 2:
                    calHome.TimeScale = System.Windows.Forms.Calendar.CalendarTimeScale.TenMinutes;
                    break;
                case 3:
                    calHome.TimeScale = System.Windows.Forms.Calendar.CalendarTimeScale.FifteenMinutes;
                    break;
                case 4:
                    calHome.TimeScale = System.Windows.Forms.Calendar.CalendarTimeScale.ThirtyMinutes;
                    break;
                case 5:
                    calHome.TimeScale = System.Windows.Forms.Calendar.CalendarTimeScale.SixtyMinutes;
                    break;
                default:
                    calHome.TimeScale = System.Windows.Forms.Calendar.CalendarTimeScale.SixtyMinutes;
                    break;
            }
        }

        private void cbCalendarPerspective_SelectedIndexChanged(object sender, EventArgs e)
        {
            switch (cbCalendarPerspective.SelectedIndex)
            {
                case 0:
                    googleCalendar.setPerspectiveMonths(1);
                    break;
                case 1:
                    googleCalendar.setPerspectiveMonths(2);
                    break;
                case 2:
                    googleCalendar.setPerspectiveMonths(3);
                    break;
                case 3:
                    googleCalendar.setPerspectiveMonths(4);
                    break;
                case 4:
                    googleCalendar.setPerspectiveMonths(6);
                    break;
                default:
                    googleCalendar.setPerspectiveMonths(1);
                    break;
            }
        }

        private void btTestGoogleConnection_Click(object sender, EventArgs e)
        {
            testConnection(true);
        }

        private void addWorkEventToolStripMenuItem_Click(object sender, EventArgs e)
        {
            openCalendarScreen(null);
        }

        private void searchToolStripMenuItem_Click(object sender, EventArgs e)
        {
            openSearchScreen();
        }

        private void calHome_ItemDoubleClick(object sender, EventArgs e)
        {

            if (calendarEventScreen == null)
            {
                calendarEventScreen = new fCalendarEvent(this, calHome.GetSelectedItems().First().getCalendarEvent());
                calendarEventScreen.Show();
            }

        }

        private void planningToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (planningScreen == null)
            {
                planningScreen = new fPlanning(this);
                planningScreen.Show();
            }
        }

        private void lbWrongTitles_SelectedIndexChanged(object sender, EventArgs e)
        {
            loadWrongTitlesControl();
        }

        private void nuHours_ValueChanged(object sender, EventArgs e)
        {
            updateNUTechHours();
        }

        private void btCancel_Click(object sender, EventArgs e)
        {
            clearWrongTitlesControl();
        }

        #endregion

        #region Getters

        public List<Technician> getTechnicianList()
        {
            return technicianList;
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

        public static String customToString(this DateTime dt)
        {
            return dt.DayOfWeek.ToString() + " " + dt.Day.ToString() + " " + dt.ToString("MMMM");
        }

    }

    public static class StringExtensions
    {
        public static bool Contains(this string source, string toCheck, StringComparison comp)
        {
            return source?.IndexOf(toCheck, comp) >= 0;
        }

        public static string CapitalizeFirst(this string input)
        {
            return char.ToUpper(input[0]) + input[1..];
        }

        public static string CapitalizeAll(this string input)
        {
            var words = input.Split(' ');

            for (int i = 0; i < words.Length; i++)
            {
                words[i] = CapitalizeFirst(words[i]);
            }

            return string.Join(' ', words);
        }

    }

    public class WrongTitleObject
    {
        private Google.Apis.Calendar.v3.Data.Event eventItem;

        public WrongTitleObject(Google.Apis.Calendar.v3.Data.Event item)
        {
            eventItem = item;
        }

        public String getId()
        {
            return eventItem.Id;
        }

        public Google.Apis.Calendar.v3.Data.Event getEvent()
        {
            return eventItem;
        }

        public override string ToString()
        {
            return eventItem.Summary.ToString();
        }

    }


}
