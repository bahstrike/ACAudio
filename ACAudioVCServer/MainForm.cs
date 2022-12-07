﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using ACACommon;
using Smith;

namespace ACAudioVCServer
{
    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();
        }

        public void Log(string s)
        {
            while (logLB.Items.Count > 100)
                logLB.Items.RemoveAt(0);

            logLB.TopIndex = logLB.Items.Add(s);
        }

        CritSect pendingLogMessagesCrit = new CritSect();
        List<string> pendingLogMessages = new List<string>();
        void LogCallback(string s)
        {
            using (pendingLogMessagesCrit.Lock)
                pendingLogMessages.Add(s);
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            Log("Startup");

            bitDepthCombo.SelectedIndex = 2;// ulaw 16bit
            sampleRateCombo.SelectedIndex = 2;//8000
            UpdateStreamInfo();//dont wait for after thread started & timer to update thread info

            Server.LogCallback = LogCallback;
            Server.Init();

        }

        void UpdateStreamInfo()
        {
            // if our current/last stream info has changed, we should issue an update
            bool ulaw;
            int bitDepth;
            int sampleRate;
            switch (bitDepthCombo.SelectedIndex)
            {
                case 0:
                    ulaw = false;
                    bitDepth = 8;
                    break;

                case 1:
                    ulaw = false;
                    bitDepth = 16;
                    break;

                default://case 2:
                    ulaw = true;
                    bitDepth = 16;
                    break;
            }

            sampleRate = int.Parse(sampleRateCombo.SelectedItem as string);

            Server.CurrentStreamInfo = new StreamInfo(ulaw, bitDepth, sampleRate);
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            using (pendingLogMessagesCrit.Lock)
            {
                foreach (string s in pendingLogMessages)
                    Log(s);
                pendingLogMessages.Clear();
            }


            UpdateStreamInfo();//in case we dont have a indexchanged or something




            Player[] players = Server.GetPlayers();

            // remove players that no longer exist
            for (int x = 0; x < playersList.Items.Count; x++)
            {
                Player player = playersList.Items[x] as Player;

                bool exist = false;
                foreach (Player p2 in players)
                    if (player.AccountName == p2.AccountName &&
                        player.CharacterName == p2.CharacterName)
                    {
                        exist = true;
                        break;
                    }

                if (!exist)
                    playersList.Items.RemoveAt(x--);
            }

            // add players we dont have
            foreach(Player player in players)
            {
                bool exist = false;
                foreach(Player p2 in playersList.Items)
                    if(player.AccountName == p2.AccountName &&
                        player.CharacterName == p2.CharacterName)
                    {
                        exist = true;
                        break;
                    }

                if (!exist)
                    playersList.Items.Add(player);
            }

            playerHeadingLabel.Text = $"Connected Players: {playersList.Items.Count}";



            sentBytes += Server.PacketsSentBytes;
            Server.PacketsSentBytes = 0;

            receivedBytes += Server.PacketsReceivedBytes;
            Server.PacketsReceivedBytes = 0;

            generalInfo.Text = $"TotalConnectAttempts:{Server.IncomingConnectionsCount}   PacketsSent:{Server.PacketsSentCount} ({sentBytes/1024}kb)  PacketsReceived:{Server.PacketsReceivedCount} ({receivedBytes/1024}kb)";
        }

        ulong receivedBytes = 0;
        ulong sentBytes = 0;

        private void MainForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            Server.Shutdown();
        }
    }
}
