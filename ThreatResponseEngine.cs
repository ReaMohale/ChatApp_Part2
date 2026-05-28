using ChatApp_Part2;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace C
{
    // ══════════════
    // Supporting models
    // ══════════════

    /// <summary>A single entry in the agent's chat log.</summary>
    public class ChatEntry
    {
        public string Speaker { get; set; } = string.Empty; // "Agent" or "Shield"
        public string Content { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }

    /// <summary>A multiple-choice security drill question.</summary>
    public class DrillQuestion
    {
        public string Prompt { get; set; } = string.Empty;
        public string[] Choices { get; set; } = Array.Empty<string>();
        public int AnswerIndex { get; set; }
        public string Debrief { get; set; } = string.Empty;
    }

    /// <summary>Emotional tone of an agent's message.</summary>
    public enum AgentMood { Calm, Alarmed, Irritated, Inquisitive }

    // ══════════════
    // ThreatResponseEngine — Part 2B NLP + memory + quiz engine
    // ══════════════
    public static class ThreatResponseEngine
    {
        // ── Session state ──────────────────────────────────────────────────
        private static string agentName = string.Empty;
        private static string lastModule = string.Empty;
        private static Random rng = new Random();

        public static List<ChatEntry> ChatLog { get; } = new List<ChatEntry>();

        private static HashSet<string> modulesAccessed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static Dictionary<string, int> moduleAccessCount = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        private static readonly string LogFile = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "secureshield_log.txt");

        // ── Drill (quiz) state ─────────────────────────────────────────────
        public static bool DrillActive { get; private set; } = false;
        private static List<DrillQuestion> drillSet;
        private static int drillCursor = 0;
        private static int drillCorrect = 0;
        public static int TotalDrills { get; private set; } = 0;
        public static int TotalCorrect { get; private set; } = 0;

        // ══════════
        // Response pools — reworded for clarity
        // ══════════

        private static readonly List<string> PhishingBriefings = new()
        {
            "🎣 ALERT: PHISHING CAMPAIGNS\n" +
            "────────────────────────────────────────────────\n" +
            "Phishing scams pretend to be trusted sources to steal logins or install malware.\n\n" +
            "Warning signs:\n" +
            " ✗ Fake urgency — \"Account closed unless you act now!\"\n" +
            " ✗ Fake domains — support@paypa1.com instead of paypal.com\n" +
            " ✗ Generic greetings — \"Dear Customer\"\n" +
            " ✗ Hidden links — hover to check the real destination\n" +
            "Action: Don’t click. Report it and delete. Type the site address yourself.",

            "🎣 ALERT: TARGETED PHISHING\n" +
            "────────────────────────────────────────────────\n" +
            "Spear phishing attacks are customized using info about you from public sources.\n\n" +
            " • Attackers use your name, workplace, and recent posts as bait.\n" +
            " • Even trained users can fall for it.\n" +
            " • Always confirm unexpected requests through another channel.\n\n" +
            "Procedure: Call the person using a known, trusted number.",

            "🎣 ALERT: PHONE & SMS SCAMS\n" +
            "────────────────────────────────────────────────\n" +
            "Phishing isn’t limited to email:\n\n" +
            " • Vishing — fake calls from banks, SARS, or IT support.\n" +
            " • Smishing — texts like \"Your package is on hold — track here.\"\n\n" +
            "Rules to follow:\n" +
            " ✗ Never share OTPs or passwords by phone or text.\n" +
            " ✓ Hang up and call the official number yourself."
        };

        private static readonly List<string> PasswordBriefings = new()
        {
            "🔑 MODULE: STRONG CREDENTIALS\n" +
            "────────────────────────────────────────────────\n" +
            "Weak passwords are the easiest way in for attackers.\n\n" +
            "How to build strong ones:\n" +
            " • Use 14+ characters — length beats complexity.\n" +
            " • Mix uppercase, lowercase, numbers, and symbols.\n" +
            " • Try passphrases like \"Blue!Ocean99#Bridge\" — strong and easy to remember.\n\n" +
            "Rules:\n" +
            " ✗ Never reuse passwords across sites.\n" +
            " ✗ Don’t share passwords, even with IT staff.\n" +
            " ✓ Use a password manager: Bitwarden, 1Password, or KeePass.",

            "🔑 MODULE: MULTI-FACTOR AUTHENTICATION\n" +
            "────────────────────────────────────────────────\n" +
            "MFA blocks access even if your password is stolen.\n\n" +
            "From strongest to weakest:\n" +
            " 1. Hardware security key (YubiKey) — resists phishing.\n" +
            " 2. Authenticator app — Google Authenticator, Microsoft Authenticator.\n" +
            " 3. SMS code — works, but can be hijacked.\n\n" +
            "Start with your email account — it controls password resets everywhere else.",

            "🔑 MODULE: PASSWORD MANAGERS\n" +
            "────────────────────────────────────────────────\n" +
            "A password manager creates and stores unique passwords for every site.\n\n" +
            "Recommended tools:\n" +
            " • Bitwarden — open source, audited, free option available.\n" +
            " • 1Password — easy to use, supports families and teams.\n" +
            " • KeePass — offline only, no cloud needed.\n\n" +
            " ✓ Check HaveIBeenPwned.com to see if your email was in a breach."
        };

        private static readonly List<string> BrowsingBriefings = new()
        {
            "🌐 GUIDE: SAFE WEB BROWSING\n" +
            "────────────────────────────────────────────────\n" +
            "Before you click or download:\n" +
            " ✓ Look for the padlock and HTTPS — HTTP sends data in plain text.\n" +
            " ✓ Hover over links to see the real URL.\n" +
            " ✓ Check for misspelled domains like g00gle.com.\n\n" +
            "While browsing:\n" +
            " ✓ Keep your browser and extensions updated.\n" +
            " ✓ Use an ad blocker to block malicious ads.\n" +
            " ✗ Don’t ignore certificate warnings — investigate first.\n\n" +
            "Downloads: Only use official websites. Check file hashes if possible.",

            "🌐 GUIDE: USING A VPN\n" +
            "────────────────────────────────────────────────\n" +
            "A VPN encrypts your connection so others on the same network can’t see your traffic.\n\n" +
            "Use it when:\n" +
            " ✓ Connecting to public Wi-Fi in airports, cafes, hotels.\n" +
            " ✓ Accessing sensitive work systems remotely.\n\n" +
            "What to look for:\n" +
            " ✓ No-logs policy verified by independent audits.\n" +
            " ✓ WireGuard or OpenVPN protocols.\n" +
            " ✗ Free VPNs often sell your data.\n\n" +
            "Note: A VPN doesn’t replace updates or safe browsing habits."
        };

        private static readonly List<string> PrivacyBriefings = new()
        {
            "🔏 BRIEFING: PROTECTING PERSONAL DATA\n" +
            "────────────────────────────────────────────────\n" +
            "Your personal info is valuable — limit what you share.\n\n" +
            "How to reduce exposure:\n" +
            " ✓ Only give the minimum details required.\n" +
            " ✓ Use a separate email for sites you don’t trust.\n" +
            " ✓ Review app permissions for camera, mic, and location every few months.\n\n" +
            "Your rights:\n" +
            " • POPIA in South Africa lets you request access, correction, or deletion of data.\n" +
            " • Send deletion requests to any company holding your info.",

            "🔏 BRIEFING: SOCIAL MEDIA RISKS\n" +
            "────────────────────────────────────────────────\n" +
            "Sharing too much online helps attackers build convincing scams.\n\n" +
            " ✓ Set profiles to private and limit who can see your posts.\n" +
            " ✓ Don’t post live locations or travel plans.\n" +
            " ✓ Treat online quizzes and polls as data collection tools.\n" +
            " ✗ Never post ID numbers, birthdays, or bank details publicly."
        };

        private static readonly List<string> MalwareBriefings = new()
        {
            "🦠 BRIEFING: TYPES OF MALWARE\n" +
            "────────────────────────────────────────────────\n" +
            "Malware is any software made to harm systems or steal data.\n\n" +
            "Common types:\n" +
            " • Ransomware — locks files and demands payment.\n" +
            " • RAT/Trojan — hides inside other software to give remote access.\n" +
            " • Keylogger — records keystrokes to steal credentials.\n" +
            " • Rootkit — hides deep in the OS to avoid detection.\n\n" +
            "Defense steps:\n" +
            " ✓ Keep your OS and apps updated.\n" +
            " ✓ Use endpoint detection and response tools.\n" +
            " ✓ Follow the 3-2-1 backup rule.\n\n" +
            "If infected: Disconnect the device from all networks right away."
        };

        private static readonly List<string> SocialEngBriefings = new()
        {
            "🎭 BRIEFING: SOCIAL ENGINEERING TACTICS\n" +
            "────────────────────────────────────────────────\n" +
            "Social engineering tricks people by exploiting trust, authority, and urgency.\n\n" +
            "Common methods:\n" +
            " • Pretexting — fake scenario to get info from you.\n" +
            " • Baiting — infected USBs or files left where you’ll find them.\n" +
            " • Tailgating — following someone into a secure area.\n" +
            " • Quid pro quo — fake help offered for access or data.\n\n" +
            "How to counter it:\n" +
            " ✓ Verify any unexpected request through a separate channel.\n" +
            " ✓ Treat urgency as a red flag — pause and check.\n" +
            " ✓ Call back using a number you know is correct."
        };

        // ══════════
        // Drill bank — reworded questions
        // ══════════
        private static readonly List<DrillQuestion> DrillBank = new()
        {
            new DrillQuestion
            {
                Prompt = "Which protocol confirms that data sent over the web is encrypted?",
                Choices = new[] { "FTP", "HTTP", "HTTPS", "SMTP" },
                AnswerIndex = 2,
                Debrief = "HTTPS uses TLS to encrypt data between your browser and the server."
            },
            new DrillQuestion
            {
                Prompt = "Which MFA method gives the strongest protection against phishing?",
                Choices = new[] { "SMS code", "Hardware security key", "Backup email code", "PIN" },
                AnswerIndex = 1,
                Debrief = "Hardware keys check the exact website domain, so phishing sites can’t trick them."
            },
            new DrillQuestion
            {
                Prompt = "You get an urgent email from 'your bank' with a link. What should you do?",
                Choices = new[] { "Click the link to secure your account", "Reply asking for details", "Go to the bank’s website directly", "Send it to a friend to check" },
                AnswerIndex = 2,
                Debrief = "Never trust links in unsolicited emails. Always type the address yourself."
            },
            new DrillQuestion
            {
                Prompt = "Why are long passphrases safer than short complex passwords?",
                Choices = new[] { "They only use special characters", "They create much higher entropy due to length", "They’re easier for servers to handle", "They change automatically" },
                AnswerIndex = 1,
                Debrief = "Each extra character massively increases the time needed to guess it."
            },
            new DrillQuestion
            {
                Prompt = "What does a VPN protect against on public Wi-Fi?",
                Choices = new[] { "Malware already on your device", "Fake DNS on remote servers", "Eavesdropping on your local network", "Browser fingerprinting" },
                AnswerIndex = 2,
                Debrief = "The VPN encrypts your connection so people on the same network can’t intercept it."
            },
            new DrillQuestion
            {
                Prompt = "You find an unknown USB drive near your desk. What’s the right move?",
                Choices = new[] { "Plug it in to see who it belongs to", "Test it on an offline PC", "Hand it to security without plugging it in", "Throw it away" },
                AnswerIndex = 2,
                Debrief = "Unknown USBs are often used in attacks. Never plug them into your computer."
            },
            new DrillQuestion
            {
                Prompt = "What’s the clearest sign of a phishing email?",
                Choices = new[] { "Clean design and logos", "Urgency pushing you to act fast", "Visible sender address", "Sent during work hours" },
                AnswerIndex = 1,
                Debrief = "Fake urgency is used to make you act before thinking. Slow down and verify."
            },
            new DrillQuestion
            {
                Prompt = "What does the 3-2-1 backup rule mean?",
                Choices = new[] { "3 backups daily, 2 weekly, 1 monthly", "3 copies, 2 storage types, 1 stored offsite", "3 TB minimum, 2 drives, 1 cloud service", "Backup 3x a week at 2 AM to 1 location" },
                AnswerIndex = 1,
                Debrief = "Keep 3 copies on 2 different media, with 1 copy stored offsite."
            },
            new DrillQuestion
            {
                Prompt = "Your PC might have malware. What’s the first thing to do?",
                Choices = new[] { "Restart and scan", "Email IT from that PC", "Disconnect it from the network", "Delete suspicious files yourself" },
                AnswerIndex = 2,
                Debrief = "Cutting network access stops the malware from communicating or spreading."
            },
            new DrillQuestion
            {
                Prompt = "What best describes social engineering?",
                Choices = new[] { "Malware spread through social media", "Tricking people to bypass security", "Scanning social sites for info", "Automated password guessing" },
                AnswerIndex = 1,
                Debrief = "It targets human behavior, not technical flaws, so awareness is key."
            }
        };

        // [All methods below remain unchanged - only text above was modified]
        public static AgentMood AnalyseMood(string input)
        {
            string l = input.ToLowerInvariant();

            string[] alarmWords = { "scared", "afraid", "worried", "anxious", "panic", "hacked", "breached", "stolen", "compromised", "attacked", "danger", "unsafe", "emergency" };
            string[] irritWords = { "annoying", "useless", "stupid", "boring", "confused", "don't understand", "makes no sense", "ridiculous", "waste", "terrible", "hate", "frustrated" };
            string[] inquireWords = { "curious", "wonder", "explain", "how does", "why does", "what is", "tell me", "learn", "interested", "show me" };

            if (alarmWords.Any(w => l.Contains(w))) return AgentMood.Alarmed;
            if (irritWords.Any(w => l.Contains(w))) return AgentMood.Irritated;
            if (inquireWords.Any(w => l.Contains(w))) return AgentMood.Inquisitive;
            return AgentMood.Calm;
        }

        public static void WriteLog(string speaker, string content)
        {
            try
            {
                string ts = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                string flat = content.Replace("\n", " ").Replace("\r", "");
                string name = string.IsNullOrEmpty(agentName) ? "Unknown" : agentName;
                File.AppendAllText(LogFile,
                    $"[{ts}] [{name}] {speaker}: {flat}{Environment.NewLine}");
            }
            catch { }
        }

        public static void WriteSessionMarker(bool starting)
        {
            try
            {
                string marker = starting ? "SESSION OPEN" : "SESSION CLOSED";
                File.AppendAllText(LogFile,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ===== {marker} ====={Environment.NewLine}");
            }
            catch { }
        }

        public static void AppendToLog(string speaker, string content)
        {
            ChatLog.Add(new ChatEntry
            {
                Speaker = speaker,
                Content = content,
                Timestamp = DateTime.Now
            });

            if (ChatLog.Count > 200) ChatLog.RemoveAt(0);
            WriteLog(speaker, content);
        }

        private static void RegisterModuleAccess(string module)
        {
            modulesAccessed.Add(module);
            moduleAccessCount[module] = moduleAccessCount.TryGetValue(module, out int c) ? c + 1 : 1;
            lastModule = module;
        }

        public static string InitialiseDrill()
        {
            drillSet = DrillBank.OrderBy(_ => rng.Next()).Take(5).ToList();
            drillCursor = 0;
            drillCorrect = 0;
            DrillActive = true;
            return FormatDrillQuestion();
        }

        public static string EvaluateDrillAnswer(int selection)
        {
            if (!DrillActive || drillSet == null || drillCursor >= drillSet.Count)
                return "No active drill. Type 'drill' to begin one.";

            DrillQuestion current = drillSet[drillCursor];
            bool isCorrect = (selection - 1) == current.AnswerIndex;
            if (isCorrect) drillCorrect++;

            string verdict = isCorrect
               ? $"✅ Correct.\n💡 Note: {current.Debrief}"
                : $"❌ Wrong. Correct answer:\n {current.Choices[current.AnswerIndex]}\n💡 Note: {current.Debrief}";

            drillCursor++;

            if (drillCursor >= drillSet.Count)
            {
                DrillActive = false;
                TotalCorrect += drillCorrect;
                TotalDrills += drillSet.Count;

                double score = (double)drillCorrect / drillSet.Count;
                string rating = score == 1.0 ? "🏆 Perfect score. Excellent work." :
                                score >= 0.8 ? "🥇 Great job — you’re well prepared." :
                                score >= 0.6 ? "👍 Pass — review the ones you missed." :
                                score >= 0.4 ? "📚 Below average — keep practicing." :
                                                "🔁 Needs work — retake the drill.";

                return $"{verdict}\n\n" +
                       $"══ DRILL FINISHED ══\n" +
                       $"Score: {drillCorrect} / {drillSet.Count}\n" +
                       $"Rating: {rating}\n\n" +
                       $"Overall: {TotalCorrect} / {TotalDrills} correct.\n\n" +
                       $"Type 'drill' to try again.";
            }

            return verdict + "\n\n" + FormatDrillQuestion();
        }

        private static string FormatDrillQuestion()
        {
            DrillQuestion q = drillSet[drillCursor];
            return
                $"📋 Question {drillCursor + 1} of {drillSet.Count}\n" +
                $"────────────────────────────────────────────────\n" +
                $"{q.Prompt}\n\n" +
                $" 1. {q.Choices[0]}\n" +
                $" 2. {q.Choices[1]}\n" +
                $" 3. {q.Choices[2]}\n" +
                $" 4. {q.Choices[3]}\n\n" +
                $"Reply with 1, 2, 3, or 4.";
        }

        public static string RetrieveChatLog(int count = 15)
        {
            if (ChatLog.Count == 0)
                return "📜 No chat history yet. Start a conversation first.";

            var recent = ChatLog.TakeLast(count).ToList();
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"📜 CHAT HISTORY (last {count} messages)");
            sb.AppendLine("────────────────────────────────────────────────");
            foreach (var entry in recent)
            {
                string label = entry.Speaker == "Agent" ? "You " : "Shield";
                string preview = entry.Content.Replace("\n", " ");
                if (preview.Length > 90) preview = preview[..90] + "…";
                sb.AppendLine($" [{entry.Timestamp:HH:mm:ss}] {label}: {preview}");
            }
            return sb.ToString();
        }

        public static string RetrieveProfileReport()
        {
            string agent = string.IsNullOrEmpty(agentName) ? "Agent" : agentName;
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"⭐ AGENT PROFILE: {agent.ToUpperInvariant()}");
            sb.AppendLine("────────────────────────────────────────────────");

            if (modulesAccessed.Count == 0)
            {
                sb.AppendLine(" No modules viewed yet.");
            }
            else
            {
                sb.AppendLine(" Modules viewed:");
                foreach (string m in modulesAccessed)
                {
                    int c = moduleAccessCount.TryGetValue(m, out int n) ? n : 0;
                    sb.AppendLine($" ★ {m} ({c} times)");
                }
            }

            sb.AppendLine();
            sb.AppendLine(TotalDrills > 0
               ? $" Drill score: {TotalCorrect} / {TotalDrills} ({Math.Round((double)TotalCorrect / TotalDrills * 100, 1)}%)"
                : " Drill score: No drills completed — type 'drill' to start.");

            return sb.ToString();
        }

        public static string Dispatch(string rawInput, SecurityBot bot)
        {
            if (string.IsNullOrWhiteSpace(rawInput)) return string.Empty;

            string lower = rawInput.ToLowerInvariant().Trim();

            if (DrillActive)
            {
                if (int.TryParse(lower, out int n) && n >= 1 && n <= 4)
                    return LogAndReturn(EvaluateDrillAnswer(n));
                return LogAndReturn("⚠️ Answer with 1, 2, 3, or 4 only.\n\n" + FormatDrillQuestion());
            }

            if (string.IsNullOrEmpty(agentName))
            {
                agentName = rawInput.Trim();
                bot.AgentName = agentName;
                WriteSessionMarker(starting: true);
                AppendToLog("Agent", rawInput);

                string brief =
                    $"Welcome, Agent {agentName}. SecureShield is ready. 🛡\n\n" +
                    "Available topics:\n\n" +
                    " 🎣 Phishing — type 'phishing'\n" +
                    " 🔑 Passwords / MFA — type 'password'\n" +
                    " 🌐 Safe Browsing — type 'browsing'\n" +
                    " 🔏 Privacy — type 'privacy'\n" +
                    " 🦠 Malware — type 'malware'\n" +
                    " 🎭 Social Engineering — type 'social'\n" +
                    " 🚨 Incident Response — type 'incident'\n" +
                    " 📋 Security Drill — type 'drill'\n" +
                    " 📜 Chat History — type 'log'\n" +
                    " ⭐ Agent Profile — type 'profile'\n\n" +
                    "You can also ask in plain English.";

                return LogAndReturn(brief);
            }

            AppendToLog("Agent", rawInput);

            if (lower is "drill" or "start drill" or "take drill" or "begin drill")
                return LogAndReturn(InitialiseDrill());

            if (lower is "log" or "comm log" or "communications log" or "history")
                return LogAndReturn(RetrieveChatLog());

            if (lower is "profile" or "my profile" or "agent profile" or "interests" or "stats")
                return LogAndReturn(RetrieveProfileReport());

            if (lower is "help" or "menu" or "commands" or "?" or "options")
                return LogAndReturn(BuildHelpMenu());

            AgentMood mood = AnalyseMood(lower);

            string moodPrefix = mood switch
            {
                AgentMood.Alarmed =>
                    $"⚠️ Understood, Agent {agentName}. Stay calm — I’ll guide you.\n\n",
                AgentMood.Irritated =>
                    $"Got it, Agent {agentName}. I’ll keep it simple.\n\n",
                AgentMood.Inquisitive =>
                    $"Good question, Agent {agentName}. Here’s the full breakdown.\n\n",
                _ => string.Empty
            };

            string repeatAlert = string.Empty;
            if (!string.IsNullOrEmpty(lastModule) &&
                moduleAccessCount.TryGetValue(lastModule, out int accessCount) && accessCount >= 2)
            {
                repeatAlert = $"\n\n💡 You’ve opened '{lastModule}' {accessCount} times. " +
                              "Try a drill to test what you know! (type 'drill')";
            }

            if (lower.Contains("more") || lower.Contains("another") ||
                lower.Contains("expand") || lower.Contains("elaborate"))
            {
                if (!string.IsNullOrEmpty(lastModule))
                    return LogAndReturn(moodPrefix + FetchExtendedBriefing(lastModule) + repeatAlert);
            }

            if (lower.Contains("how are you") || lower.Contains("status") || lower.Contains("you okay"))
                return LogAndReturn($"🟢 SecureShield is online, Agent {agentName}. All systems normal. What do you need?");

            if (lower.Contains("purpose") || lower.Contains("what are you") || lower.Contains("who are you"))
                return LogAndReturn($"🛡 SecureShield helps Agent {agentName} stay safe online.\n\nCovers: phishing, passwords, browsing, malware, social engineering, privacy, and incident response.");

            if (lower.Contains("what can") || lower.Contains("what do you"))
                return LogAndReturn(BuildHelpMenu());

            if (lower.Contains("phish") || lower.Contains("scam") || lower.Contains("fake email") ||
                lower.Contains("vish") || lower.Contains("smish") || lower.Contains("spam"))
            {
                RegisterModuleAccess("phishing");
                return LogAndReturn(moodPrefix + PhishingBriefings[rng.Next(PhishingBriefings.Count)] + repeatAlert);
            }

            if (lower.Contains("password") || lower.Contains("credential") || lower.Contains("mfa") ||
                lower.Contains("2fa") || lower.Contains("passphrase") || lower.Contains("login") ||
                lower.Contains("two factor") || lower.Contains("vault"))
            {
                RegisterModuleAccess("credentials");
                return LogAndReturn(moodPrefix + PasswordBriefings[rng.Next(PasswordBriefings.Count)] + repeatAlert);
            }

            if (lower.Contains("brows") || lower.Contains("https") || lower.Contains("vpn") ||
                lower.Contains("wifi") || lower.Contains("wi-fi") || lower.Contains("website") ||
                lower.Contains("url") || lower.Contains("link") || lower.Contains("internet"))
            {
                RegisterModuleAccess("secure browsing");
                return LogAndReturn(moodPrefix + BrowsingBriefings[rng.Next(BrowsingBriefings.Count)] + repeatAlert);
            }

            if (lower.Contains("privacy") || lower.Contains("personal data") || lower.Contains("popia") ||
                lower.Contains("gdpr") || lower.Contains("tracking") || lower.Contains("social media") ||
                lower.Contains("data protection"))
            {
                RegisterModuleAccess("privacy");
                return LogAndReturn(moodPrefix + PrivacyBriefings[rng.Next(PrivacyBriefings.Count)] + repeatAlert);
            }

            if (lower.Contains("malware") || lower.Contains("virus") || lower.Contains("ransomware") ||
                lower.Contains("trojan") || lower.Contains("keylogger") || lower.Contains("rootkit") ||
                lower.Contains("spyware") || lower.Contains("infected") || lower.Contains("antivirus"))
            {
                RegisterModuleAccess("malware");
                return LogAndReturn(moodPrefix + MalwareBriefings[rng.Next(MalwareBriefings.Count)] + repeatAlert);
            }

            if (lower.Contains("social engineer") || lower.Contains("pretex") || lower.Contains("baiting") ||
                lower.Contains("tailgat") || lower.Contains("manipulation") || lower.Contains("usb drive"))
            {
                RegisterModuleAccess("social engineering");
                return LogAndReturn(moodPrefix + SocialEngBriefings[rng.Next(SocialEngBriefings.Count)] + repeatAlert);
            }

            if (lower.Contains("incident") || lower.Contains("hacked") || lower.Contains("breached") ||
                lower.Contains("compromised") || lower.Contains("account stolen") || lower.Contains("what do i do"))
            {
                RegisterModuleAccess("incident response");
                return LogAndReturn(moodPrefix + BuildIncidentProtocol() + repeatAlert);
            }

            string contextHint = modulesAccessed.Count > 0
               ? $"\n\nRecent topics: {string.Join(", ", modulesAccessed)}. Type one to continue."
                : string.Empty;

            string[] fallbacks = {
                $"❓ I didn’t get that, Agent {agentName}. Try rephrasing or type 'help'.{contextHint}",
                $"🔍 I couldn’t understand that. Try 'phishing', 'malware', or 'drill'.{contextHint}",
                $"⚠️ Unknown input. Type a topic or ask a question.{contextHint}"
            };
            return LogAndReturn(fallbacks[rng.Next(fallbacks.Length)]);
        }

        private static string LogAndReturn(string response)
        {
            AppendToLog("Shield", response);
            return response;
        }

        private static string FetchExtendedBriefing(string module) => module switch
        {
            "phishing" => PhishingBriefings[rng.Next(PhishingBriefings.Count)],
            "credentials" => PasswordBriefings[rng.Next(PasswordBriefings.Count)],
            "secure browsing" => BrowsingBriefings[rng.Next(BrowsingBriefings.Count)],
            "privacy" => PrivacyBriefings[rng.Next(PrivacyBriefings.Count)],
            "malware" => MalwareBriefings[rng.Next(MalwareBriefings.Count)],
            "social engineering" => SocialEngBriefings[rng.Next(SocialEngBriefings.Count)],
            _ => "Pick a topic for more detail: phishing, credentials, browsing, malware, social, or privacy."
        };

        private static string BuildIncidentProtocol() =>
            "🚨 INCIDENT RESPONSE STEPS\n" +
            "────────────────────────────────────────────────\n" +
            "If you think you’ve been hacked, do this now:\n\n" +
            " 1. Change all passwords — start with email.\n" +
            " 2. Turn on MFA everywhere you can.\n" +
            " 3. Log out of unknown sessions.\n" +
            " 4. Contact your bank if money or cards are involved.\n" +
            " 5. Run a full antivirus scan.\n" +
            " 6. Report it to your local cybercrime unit.\n" +
            " 7. Check HaveIBeenPwned.com for your email.\n\n" +
            "Act fast — attackers move quickly after gaining access.";

        private static string BuildHelpMenu()
        {
            string agent = string.IsNullOrEmpty(agentName) ? "Agent" : agentName;
            return
                $"📋 SECURESHIELD HELP MENU — Agent {agent}\n\n" +
                " 🎣 Phishing & scams — type 'phishing'\n" +
                " 🔑 Passwords & MFA — type 'password'\n" +
                " 🌐 Safe browsing & VPN — type 'browsing'\n" +
                " 🔏 Data privacy — type 'privacy'\n" +
                " 🦠 Malware & ransomware — type 'malware'\n" +
                " 🎭 Social engineering — type 'social'\n" +
                " 🚨 Incident response — type 'incident'\n" +
                " 📋 Security drill — type 'drill'\n" +
                " 📜 Chat history — type 'log'\n" +
                " ⭐ Agent profile & stats — type 'profile'\n\n" +
                "You can also just ask me in plain language.";
        }
    }
}