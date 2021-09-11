﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace JugaAgenda_v2
{
    public partial class fCalendarEvent : Form
    {
        private DateTime prevDate;
        private fHome mainScreen;
        private Google.Apis.Calendar.v3.Data.Event oldWorkEvent;

        public fCalendarEvent(fHome mainScreen, Google.Apis.Calendar.v3.Data.Event oldWorkEvent = null)
        {
            this.mainScreen = mainScreen;
            this.oldWorkEvent = oldWorkEvent;

			InitializeComponent();
            loadComponents();

            if (oldWorkEvent != null) loadWorkEvent();
        }

        private void loadComponents()
        {
            cbFullDays.Checked = true;

            dtpStart.Value = DateTime.Now;
            dtpEnd.Value = DateTime.Now;
            prevDate = dtpStart.Value;

            for (int i = 0; i < 24; i++) cbHourStart.Items.Add(i);
            cbHourStart.SelectedIndex = 0;
            cbHourStart.Enabled = false;
            for (int i = 0; i < 60; i += 5) cbMinuteStart.Items.Add(i);
            cbMinuteStart.SelectedIndex = 0;
            cbMinuteStart.Enabled = false;
            for (int i = 0; i < 24; i++) cbHourEnd.Items.Add(i);
            cbHourEnd.SelectedIndex = 0;
            cbHourEnd.Enabled = false;
            for (int i = 0; i < 60; i += 5) cbMinuteEnd.Items.Add(i);
            cbMinuteEnd.SelectedIndex = 0;
            cbMinuteEnd.Enabled = false;

            foreach (Work.Status status in Enum.GetValues(typeof(Work.Status))) cbStatus.Items.Add(status);
            cbStatus.SelectedIndex = 0;

        }

        private void loadWorkEvent()
        {
            if (oldWorkEvent.Start.DateTime != null)
            {
                cbFullDays.Checked = false;
                dtpStart.Value = (DateTime)oldWorkEvent.Start.DateTime;
                dtpEnd.Value = (DateTime)oldWorkEvent.End.DateTime;
                cbHourStart.SelectedIndex = ((DateTime)oldWorkEvent.Start.DateTime).Hour;
                cbHourEnd.SelectedIndex = ((DateTime)oldWorkEvent.End.DateTime).Hour;
                cbMinuteStart.SelectedIndex = Convert.ToInt32(Math.Floor((decimal)((DateTime)oldWorkEvent.Start.DateTime).Minute / 5));
                cbMinuteEnd.SelectedIndex = Convert.ToInt32(Math.Floor((decimal)((DateTime)oldWorkEvent.End.DateTime).Minute / 5));
                cbHourStart.Enabled = true;
                cbMinuteStart.Enabled = true;
                cbHourEnd.Enabled = true;
                cbMinuteEnd.Enabled = true;
            } else
            {
                cbFullDays.Checked = true;
                dtpStart.Value = Convert.ToDateTime(oldWorkEvent.Start.Date);
                dtpEnd.Value = Convert.ToDateTime(oldWorkEvent.End.Date).AddDays(-1);
                cbHourStart.Enabled = false;
                cbMinuteStart.Enabled = false;
                cbHourEnd.Enabled = false;
                cbMinuteEnd.Enabled = false;
            }

            cbStatus.SelectedItem = new Work().colorID_to_status(Convert.ToInt32(oldWorkEvent.ColorId));

            String[] title = oldWorkEvent.Summary.Split(' ');
            for (int i = 0; i < title.Length; i++)
            {
                if (i == 0) nuHours.Value = Convert.ToDecimal(title[i].Split('u')[0].Replace('.', ','));
                if (i == 1) tbClientName.Text = title[i];
                if (i == 2) tbPhoneNumber.Text = title[i];
                if (i == 3) tbOrderNumber.Text = title[i];
            }

            rtbDescription.Text = oldWorkEvent.Description;

        }

        private void cbFullDays_CheckedChanged(object sender, EventArgs e)
        {
            if (cbFullDays.Checked)
            {
                cbHourStart.Enabled = false;
                cbMinuteStart.Enabled = false;
                cbHourEnd.Enabled = false;
                cbMinuteEnd.Enabled = false;
            } else
            {
                cbHourStart.Enabled = true;
                cbMinuteStart.Enabled = true;
                cbHourEnd.Enabled = true;
                cbMinuteEnd.Enabled = true;
            }
        }

        private bool checkValues()
        {
            if (DateTime.Compare(dtpStart.Value.Date, dtpEnd.Value.Date) > 0)
            {
                MessageBox.Show("Dates are incorrect.");
                return false;
            }
            if (!cbFullDays.Checked && DateTime.Compare(dtpStart.Value.Date.AddHours(Convert.ToInt64(cbHourStart.SelectedItem)).AddMinutes(Convert.ToInt64(cbMinuteStart.SelectedItem)), dtpEnd.Value.Date.AddHours(Convert.ToInt64(cbHourEnd.SelectedItem)).AddMinutes(Convert.ToInt64(cbMinuteEnd.SelectedItem))) >= 0)
            {
                MessageBox.Show("Times are incorrect.");
                return false;
            }
            if (tbClientName.Text.Length <= 0)
            {
                MessageBox.Show("Client name is incorrect.");
                return false;
            }
            if (tbPhoneNumber.Text.Length <= 0)
            {
                MessageBox.Show("Phone number is incorrect.");
                return false;
            }
            return true;
        }

        private void dtpStart_ValueChanged(object sender, EventArgs e)
        {
            dtpEnd.Value += (dtpStart.Value - prevDate);
            prevDate = dtpStart.Value;
        }

        private void btCancel_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void btSave_Click(object sender, EventArgs e)
        {
            if (checkValues())
            {
                if (this.oldWorkEvent == null)
                {
                    String title = nuHours.Value.ToString() + "u " + tbClientName.Text.ToString().Replace(' ', '_') + " " + tbPhoneNumber.Text.ToString().Replace(' ', '_');
                    if (tbOrderNumber.Text.Length > 0) title += " " + tbOrderNumber.Text.ToString().Replace(' ', '_');

                    if (cbFullDays.Checked)
                    {
                        if (mainScreen.googleCalendar.addWorkEvent(title, rtbDescription.Text.ToString(), dtpStart.Value.ToString("yyyy-MM-dd"), dtpEnd.Value.AddDays(1).ToString("yyyy-MM-dd"), new Work().status_to_colorID((Work.Status)cbStatus.SelectedItem).ToString()))
                        {
                            this.Close();
                        } else
                        {
                            MessageBox.Show("Something went wrong when adding new event to calendar.");
                        }
                    } else
                    {
                        if (mainScreen.googleCalendar.addWorkEvent(title, rtbDescription.Text.ToString(), dtpStart.Value.Date.AddHours(Convert.ToInt64(cbHourStart.SelectedItem)).AddMinutes(Convert.ToInt64(cbMinuteStart.SelectedItem)), dtpEnd.Value.Date.AddHours(Convert.ToInt64(cbHourEnd.SelectedItem)).AddMinutes(Convert.ToInt64(cbMinuteEnd.SelectedItem)), new Work().status_to_colorID((Work.Status)cbStatus.SelectedItem).ToString()))
                        {
                            this.Close();
                        }
                        else
                        {
                            MessageBox.Show("Something went wrong when adding new event to calendar.");
                        }
                    }
                } else
                {
                    String title = nuHours.Value.ToString() + "u " + tbClientName.Text.ToString().Replace(' ', '_') + " " + tbPhoneNumber.Text.ToString().Replace(' ', '_');
                    if (tbOrderNumber.Text.Length > 0) title += " " + tbOrderNumber.Text.ToString().Replace(' ', '_');

                    if (cbFullDays.Checked)
                    {
                        if (mainScreen.googleCalendar.editWorkEvent(title, rtbDescription.Text.ToString(), dtpStart.Value.ToString("yyyy-MM-dd"), dtpEnd.Value.AddDays(1).ToString("yyyy-MM-dd"), new Work().status_to_colorID((Work.Status)cbStatus.SelectedItem).ToString(), oldWorkEvent.Id))
                        {
                            this.Close();
                        }
                        else
                        {
                            MessageBox.Show("Something went wrong when adding new event to calendar.");
                        }
                    }
                    else
                    {
                        if (mainScreen.googleCalendar.editWorkEvent(title, rtbDescription.Text.ToString(), dtpStart.Value.Date.AddHours(Convert.ToInt64(cbHourStart.SelectedItem)).AddMinutes(Convert.ToInt64(cbMinuteStart.SelectedItem)), dtpEnd.Value.Date.AddHours(Convert.ToInt64(cbHourEnd.SelectedItem)).AddMinutes(Convert.ToInt64(cbMinuteEnd.SelectedItem)), new Work().status_to_colorID((Work.Status)cbStatus.SelectedItem).ToString(), oldWorkEvent.Id))
                        {
                            this.Close();
                        }
                        else
                        {
                            MessageBox.Show("Something went wrong when adding new event to calendar.");
                        }
                    }
                }
                
            }
        }
    }
}
