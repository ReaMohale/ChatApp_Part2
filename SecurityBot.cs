using System;
using System.Media;
using System.Threading;

namespace ChatApp_Part2
{
    /// <summary>
    /// SecurityBot — the core bot class for Part 2B.
    /// Same responsibilities as CyberBot from Part 1 but with
    /// reworded methods and a distinct identity.
    /// </summary>
    public class SecurityBot
    {
        private string agentName = string.Empty;

        // Public property so the GUI can read/write the agent name
        public string AgentName
        {
            get => agentName;
            set => agentName = value;
        }

        // Event the GUI can subscribe to for receiving bot output
        public event Action<string>? OnBotOutput;

        /// <summary>
        /// Launches the bot — prints the banner, plays audio,
        /// collects the agent name, then greets.
        /// (Console path; GUI calls individual methods directly.)
        /// </summary>
        public void Launch()
        {
            Console.Title = "🛡 SecureShield Awareness System";
            PrintBanner();
            PlayStartupAudio();
            CollectAgentName();
            GreetAgent();
        }

        // ── Banner ────────────────────────────────────────────────────────
        public void PrintBanner()
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("*=================================================*");
            Console.WriteLine("*======= CYBERSECURITY AWARENESS SYSTEM ==========*");
            Console.WriteLine("*=================================================*");
            Console.WriteLine(@"
 ____                           ____  _     _      _     _ 
/ ___|  ___  ___ _   _ _ __ ___/ ___|| |__ (_) ___| | __| |
\___ \ / _ \/ __| | | | '__/ _ \___ \| '_ \| |/ _ \ |/ _` |
 ___) |  __/ (__| |_| | | |  __/___) | | | | |  __/ | (_| |
|____/ \___|\___|\__,_|_|  \___|____/|_| |_|_|\___|_|\__,_|   
        'Protecting you in the Digital Age'  
");
            Console.ResetColor();
        }

        // ── Startup audio ─────────────────────────────────────────────────
        public void PlayStartupAudio()
        {
            try
            {
                string path = "welcome.wav";
                SoundPlayer player = new SoundPlayer(path);
                player.Load();
                player.PlaySync();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("⚠ Startup audio unavailable: " + ex.Message);
                Console.ResetColor();
            }
        }

        // ──  name collection ─────────────────────────────────────────
        public void CollectAgentName()
        {
            Console.Write("\nEnter your agent name: ");
            agentName = Console.ReadLine() ?? string.Empty;

            while (string.IsNullOrWhiteSpace(agentName))
            {
                Console.Write("Agent name cannot be blank. Try again: ");
                agentName = Console.ReadLine() ?? string.Empty;
            }
        }

        // ── Greeting ──────────────────────────────────────────────────────
        public void GreetAgent()
        {
            StreamText($"\nAgent {agentName}, SecureShield is online and ready.");
            StreamText("Your digital safety is our mission.\n");
        }

        // ── Streaming text effect (typing effect equivalent) ──────────────
        public void StreamText(string message)
        {
            foreach (char ch in message)
            {
                Console.Write(ch);
                Thread.Sleep(12);
            }
            Console.WriteLine();
        }

        // ── Alert display ─────────────────────────────────────────────────
        private void DisplayAlert(string message)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("[!] " + message);
            Console.ResetColor();
        }
    }
}