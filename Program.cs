using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Linq;
using System.Threading.Tasks;

namespace VortexBot
{
    class Program
    {
        // ==========================================
        // ⚠️ PALITAN MO MUNA ITO!
        // ==========================================
        private const ulong OWNER_ID = 1432638241177075827;  // ← ILAGAY ANG DISCORD ID MO
        private const string DATA_FILE = "vortex_data.json";
        private const long NEW_PLAYER_BALANCE = 1000000;

        private DiscordSocketClient _client = null!;
        private readonly Random _random = new();
        private readonly Dictionary<ulong, long> _balances = new();
        private readonly Dictionary<ulong, DateTime> _dailyCooldown = new();
        private readonly Dictionary<ulong, DateTime> _workCooldown = new();
        private readonly Dictionary<ulong, DateTime> _jailedUsers = new(); // Jail System Storage

        private class BotData
        {
            public Dictionary<ulong, long> Balances { get; set; } = new();
            public Dictionary<ulong, DateTime> DailyCooldown { get; set; } = new();
            public Dictionary<ulong, DateTime> WorkCooldown { get; set; } = new();
            public Dictionary<ulong, DateTime> JailedUsers { get; set; } = new();
        }

        static void Main(string[] args)
        {
            new Program().MainAsync().GetAwaiter().GetResult();
        }

        public async Task MainAsync()
        {
            LoadData();

            var config = new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent | GatewayIntents.GuildMembers
            };

            _client = new DiscordSocketClient(config);
            _client.Log += Log;
            _client.Ready += OnReady;
            _client.MessageReceived += MessageHandler;

            // ==========================================
            // 🔑 ILAGAY ANG BOT TOKEN MO DITO
            // ==========================================
            string token = Environment.GetEnvironmentVariable("TOKEN");

            await _client.LoginAsync(TokenType.Bot, token);
            await _client.StartAsync();
            await Task.Delay(-1);
        }

        private Task OnReady()
        {
            Console.WriteLine("========================================");
            Console.WriteLine("🌪️  VORTEX BOT — ONLINE & READY!");
            Console.WriteLine($"✅ Logged in as: {_client.CurrentUser}");
            Console.WriteLine("========================================");
            return Task.CompletedTask;
        }

        private void SaveData()
        {
            try
            {
                var data = new BotData
                {
                    Balances = _balances,
                    DailyCooldown = _dailyCooldown,
                    WorkCooldown = _workCooldown,
                    JailedUsers = _jailedUsers
                };
                File.WriteAllText(DATA_FILE, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex) { Console.WriteLine($"Save Error: {ex.Message}"); }
        }

        private void LoadData()
        {
            try
            {
                if (!File.Exists(DATA_FILE)) return;
                var data = JsonSerializer.Deserialize<BotData>(File.ReadAllText(DATA_FILE));
                if (data != null)
                {
                    foreach (var kvp in data.Balances) _balances[kvp.Key] = kvp.Value;
                    foreach (var kvp in data.DailyCooldown) _dailyCooldown[kvp.Key] = kvp.Value;
                    foreach (var kvp in data.WorkCooldown) _workCooldown[kvp.Key] = kvp.Value;
                    foreach (var kvp in data.JailedUsers) _jailedUsers[kvp.Key] = kvp.Value;
                }
            }
            catch (Exception ex) { Console.WriteLine($"Load Error: {ex.Message}"); }
        }

        private void EnsureAccount(ulong userId)
        {
            if (!_balances.ContainsKey(userId))
                _balances[userId] = userId == OWNER_ID ? 100000000000000000 : NEW_PLAYER_BALANCE;
        }

        // ==========================================
        // 🎨 GRADIENT HELPER FUNCTIONS
        // ==========================================
        private string HexFromRgb(int r, int g, int b)
        {
            return $"#{r:X2}{g:X2}{b:X2}";
        }

        private (int r, int g, int b) RgbFromHex(string hexColor)
        {
            try
            {
                if (hexColor.StartsWith("#")) hexColor = hexColor.TrimStart('#');
                int r = Convert.ToInt32(hexColor.Substring(0, 2), 16);
                int g = Convert.ToInt32(hexColor.Substring(2, 2), 16);
                int b = Convert.ToInt32(hexColor.Substring(4, 2), 16);
                return (r, g, b);
            }
            catch { return (255, 0, 0); }
        }

        // ==========================================
        // 👑 OWNER-ONLY CHECK
        // ==========================================
        private bool IsOwner(ulong userId) => userId == OWNER_ID;

        private async Task MessageHandler(SocketMessage message)
        {
            if (message.Author.IsBot) return;
            string cmd = message.Content.Trim();
            if (string.IsNullOrWhiteSpace(cmd) || !cmd.StartsWith("!")) return;

            ulong userId = message.Author.Id;
            EnsureAccount(userId);

            // ==========================================
            // 🎨 !GRADIENT — GENERATE GRADIENT TEXT
            // ==========================================
            if (cmd.StartsWith("!gradient ", StringComparison.OrdinalIgnoreCase))
            {
                string[] parts = cmd.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2)
                {
                    await message.Channel.SendMessageAsync(
                        "🎨 **Gradient Text Generator**\n" +
                        "Usage: `!gradient <Text> <StartColor> <EndColor>`\n" +
                        "Example: `!gradient jher #FF0000 #000000`"
                    );
                    return;
                }

                string text = parts[1];
                string startHex = parts.Length >= 3 ? parts[2] : "#FF0000";
                string endHex = parts.Length >= 4 ? parts[3] : "#FFFFFF";

                var startRgb = RgbFromHex(startHex);
                var endRgb = RgbFromHex(endHex);

                int len = text.Length;
                string gradientOutput = "";

                for (int i = 0; i < len; i++)
                {
                    float progress = len == 1 ? 0 : (float)i / (len - 1);
                    int r = (int)(startRgb.r + (endRgb.r - startRgb.r) * progress);
                    int g = (int)(startRgb.g + (endRgb.g - startRgb.g) * progress);
                    int b = (int)(startRgb.b + (endRgb.b - startRgb.b) * progress);

                    string colorHex = HexFromRgb(r, g, b);
                    gradientOutput += $"<font color=\"{colorHex}\">{text[i]}</font>";
                }

                var embed = new EmbedBuilder()
                    .WithTitle("🎨 Gradient Generated")
                    .AddField("Text", text)
                    .AddField("Start", startHex)
                    .AddField("End", endHex)
                    .AddField("Generated Rich Text", $"```html\n{gradientOutput}\n```")
                    .WithColor(new Color(startRgb.r, startRgb.g, startRgb.b))
                    .WithFooter("VORTEX Gradient Generator")
                    .Build();

                await message.Channel.SendMessageAsync(embed: embed);
                return;
            }

            // ==========================================
            // 💰 ECONOMY COMMANDS
            // ==========================================

            // !balance / !bal
            if (cmd == "!balance" || cmd == "!bal")
            {
                long bal = _balances[userId];
                await message.Channel.SendMessageAsync($"💰 Your Balance: **{bal:N0} Vortex Coins**");
                return;
            }

            // !daily
            if (cmd == "!daily")
            {
                var now = DateTime.UtcNow;
                if (_dailyCooldown.TryGetValue(userId, out var last) && (now - last).TotalHours < 24)
                {
                    var rem = TimeSpan.FromHours(24) - (now - last);
                    await message.Channel.SendMessageAsync($"⏳ Come back in **{rem.Hours}h {rem.Minutes}m**");
                    return;
                }
                int reward = _random.Next(500, 1001);
                _balances[userId] += reward;
                _dailyCooldown[userId] = now;
                SaveData();
                await message.Channel.SendMessageAsync($"🎁 Claimed! +**{reward:N0} Coins!**");
                return;
            }

            // !work
            if (cmd == "!work")
            {
                var now = DateTime.UtcNow;
                if (_workCooldown.TryGetValue(userId, out var last) && (now - last).TotalMinutes < 5)
                {
                    var rem = TimeSpan.FromMinutes(5) - (now - last);
                    await message.Channel.SendMessageAsync($"⏳ Cooldown: **{rem.Seconds}s**");
                    return;
                }
                string[] jobs = { "Coded new features", "Maintained servers", "Inspected engines", "Secured perimeter", "Calibrated systems" };
                int reward = _random.Next(100, 401);
                _balances[userId] += reward;
                _workCooldown[userId] = now;
                SaveData();
                await message.Channel.SendMessageAsync($"🛠️ {jobs[_random.Next(jobs.Length)]}\n💰 Earned: +**{reward:N0} Coins!**");
                return;
            }

            // !leaderboard / !lb
            if (cmd == "!leaderboard" || cmd == "!lb")
            {
                var top10 = _balances.OrderByDescending(x => x.Value).Take(10).ToList();
                string lbText = "🏆 **Top 10 Richest**\n";
                for (int i = 0; i < top10.Count; i++)
                {
                    var user = _client.GetUser(top10[i].Key);
                    string medal = i switch { 0 => "🥇", 1 => "🥈", 2 => "🥉", _ => "🏅" };
                    lbText += $"{medal} {user?.Username ?? "User"} — **{top10[i].Value:N0} Coins**\n";
                }
                await message.Channel.SendMessageAsync(lbText);
                return;
            }

            // ==========================================
            // 🎮 ARCADE GAMES
            // ==========================================

            // !coinflip heads/tails
            if (cmd.StartsWith("!coinflip ", StringComparison.OrdinalIgnoreCase))
            {
                string[] p = cmd.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (p.Length < 2 || (p[1].ToLower() != "heads" && p[1].ToLower() != "tails"))
                {
                    await message.Channel.SendMessageAsync("🎮 Usage: `!coinflip heads` or `!coinflip tails`");
                    return;
                }
                string res = _random.Next(2) == 0 ? "heads" : "tails";
                string emoji = res == "heads" ? "🪙" : "🦅";
                string resultText = p[1].ToLower() == res ? "🎉 **YOU WIN!**" : "😔 **YOU LOST!**";
                await message.Channel.SendMessageAsync($"{emoji} Flipped: **{res.ToUpper()}**!\n{resultText}");
                return;
            }

            // !dice
            if (cmd == "!dice")
            {
                int roll = _random.Next(1, 7);
                await message.Channel.SendMessageAsync($"🎲 Rolled: **{roll}**");
                return;
            }

            // !rps rock/paper/scissors
            if (cmd.StartsWith("!rps ", StringComparison.OrdinalIgnoreCase))
            {
                string[] p = cmd.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (p.Length < 2)
                {
                    await message.Channel.SendMessageAsync("✂️ Usage: `!rps rock/paper/scissors`");
                    return;
                }
                string[] choices = { "rock", "paper", "scissors" };
                string player = p[1].ToLower();
                if (!choices.Contains(player))
                {
                    await message.Channel.SendMessageAsync("❌ Choose: `rock`, `paper`, or `scissors`");
                    return;
                }
                string bot = choices[_random.Next(3)];
                string emojiP = player == "rock" ? "🪨" : player == "paper" ? "📄" : "✂️";
                string emojiB = bot == "rock" ? "🪨" : bot == "paper" ? "📄" : "✂️";
                string result;
                if (player == bot) result = "🤝 **IT'S A TIE!**";
                else if ((player == "rock" && bot == "scissors") || (player == "paper" && bot == "rock") || (player == "scissors" && bot == "paper"))
                    result = "🎉 **YOU WIN!**";
                else result = "😔 **YOU LOST!**";
                await message.Channel.SendMessageAsync($"{emojiP} You: **{player.ToUpper()}** vs {emojiB} Bot: **{bot.ToUpper()}**\n{result}");
                return;
            }

            // !guess
            if (cmd.StartsWith("!guess ", StringComparison.OrdinalIgnoreCase))
            {
                string[] p = cmd.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (p.Length < 2 || !int.TryParse(p[1], out int guess) || guess < 1 || guess > 10)
                {
                    await message.Channel.SendMessageAsync("🔢 Guess a number 1-10: `!guess 5`");
                    return;
                }
                int num = _random.Next(1, 11);
                if (guess == num)
                    await message.Channel.SendMessageAsync($"🎉 **CORRECT!** It was {num}!");
                else
                    await message.Channel.SendMessageAsync($"❌ Wrong! It was {num}. Try again!");
                return;
            }

            // !slots
            if (cmd == "!slots")
            {
                string[] sym = { "🍒", "🍋", "🍇", "⭐", "💎", "7️⃣" };
                string a = sym[_random.Next(sym.Length)], b = sym[_random.Next(sym.Length)], c = sym[_random.Next(sym.Length)];
                string result = a == b && b == c ? "🎉 **JACKPOT!**" : (a == b || b == c || a == c) ? "✨ **Nice!**" : "😔 Try again";
                await message.Channel.SendMessageAsync($"```\n🎰 [ {a} ] [ {b} ] [ {c} ]\n```{result}");
                return;
            }

            // !arcade
            if (cmd == "!arcade")
            {
                await message.Channel.SendMessageAsync(
                    "🎮 **ARCADE GAMES**\n" +
                    "`!coinflip heads/tails` — Coinflip game\n" +
                    "`!dice` — Roll the dice\n" +
                    "`!rps rock/paper/scissors` — Rock Paper Scissors\n" +
                    "`!guess 1-10` — Guess a number\n" +
                    "`!slots` — Free arcade slots"
                );
                return;
            }

            // ==========================================
            // 👮‍♂️ MODERATION SYSTEM — KICK / JAIL / BAN
            // ==========================================

            // !kick @User [Reason]
            if (cmd.StartsWith("!kick ", StringComparison.OrdinalIgnoreCase))
            {
                if (!IsOwner(userId)) { await message.Channel.SendMessageAsync("❌ **FORBIDDEN** — Owner only!"); return; }
                string[] p = cmd.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (p.Length < 2) { await message.Channel.SendMessageAsync("✅ Usage: `!kick @User [Reason]`"); return; }
                if (!MentionUtils.TryParseUser(p[1], out ulong tid)) { await message.Channel.SendMessageAsync("❌ Invalid user"); return; }

                var guildUser = message.Channel is IGuildChannel gc ? await gc.Guild.GetUserAsync(tid) : null;
                if (guildUser == null) { await message.Channel.SendMessageAsync("❌ User not found in this server"); return; }
                if (guildUser.Id == userId) { await message.Channel.SendMessageAsync("❌ Cannot kick yourself"); return; }

                string reason = p.Length >= 3 ? string.Join(" ", p.Skip(2)) : "No reason given";
                await guildUser.KickAsync(reason);
                await message.Channel.SendMessageAsync($"👢 **KICKED!**\n👤 User: <@{tid}>\n📝 Reason: {reason}");
                return;
            }

            // !jail @User [Minutes]
            if (cmd.StartsWith("!jail ", StringComparison.OrdinalIgnoreCase))
            {
                if (!IsOwner(userId)) { await message.Channel.SendMessageAsync("❌ **FORBIDDEN** — Owner only!"); return; }
                string[] p = cmd.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (p.Length < 2) { await message.Channel.SendMessageAsync("✅ Usage: `!jail @User [minutes]`\nDefault: 60 minutes"); return; }
                if (!MentionUtils.TryParseUser(p[1], out ulong tid)) { await message.Channel.SendMessageAsync("❌ Invalid user"); return; }

                int minutes = p.Length >= 3 && int.TryParse(p[2], out int m) && m > 0 ? m : 60;
                _jailedUsers[tid] = DateTime.UtcNow.AddMinutes(minutes);
                SaveData();
                await message.Channel.SendMessageAsync($"⛓️ **JAILED!**\n👤 User: <@{tid}>\n⏳ Duration: **{minutes} minute(s)**");
                return;
            }

            // !unjail @User
            if (cmd.StartsWith("!unjail ", StringComparison.OrdinalIgnoreCase))
            {
                if (!IsOwner(userId)) { await message.Channel.SendMessageAsync("❌ **FORBIDDEN** — Owner only!"); return; }
                string[] p = cmd.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (p.Length < 2) { await message.Channel.SendMessageAsync("✅ Usage: `!unjail @User`"); return; }
                if (!MentionUtils.TryParseUser(p[1], out ulong tid)) { await message.Channel.SendMessageAsync("❌ Invalid user"); return; }

                if (_jailedUsers.Remove(tid))
                {
                    SaveData();
                    await message.Channel.SendMessageAsync($"🔓 **UNJAILED!** <@{tid}> is free!");
                }
                else
                {
                    await message.Channel.SendMessageAsync("❌ User is not jailed");
                }
                return;
            }

            // !ban @User [Reason]
            if (cmd.StartsWith("!ban ", StringComparison.OrdinalIgnoreCase))
            {
                if (!IsOwner(userId)) { await message.Channel.SendMessageAsync("❌ **FORBIDDEN** — Owner only!"); return; }
                string[] p = cmd.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (p.Length < 2) { await message.Channel.SendMessageAsync("✅ Usage: `!ban @User [Reason]`"); return; }
                if (!MentionUtils.TryParseUser(p[1], out ulong tid)) { await message.Channel.SendMessageAsync("❌ Invalid user"); return; }

                var guildUser = message.Channel is IGuildChannel gc ? await gc.Guild.GetUserAsync(tid) : null;
                if (guildUser == null) { await message.Channel.SendMessageAsync("❌ User not found in this server"); return; }
                if (guildUser.Id == userId) { await message.Channel.SendMessageAsync("❌ Cannot ban yourself"); return; }

                string reason = p.Length >= 3 ? string.Join(" ", p.Skip(2)) : "No reason given";
                await guildUser.Guild.AddBanAsync(tid, 0, reason);
                await message.Channel.SendMessageAsync($"🚫 **BANNED!**\n👤 User: <@{tid}>\n📝 Reason: {reason}");
                return;
            }

            // !unban @User
            if (cmd.StartsWith("!unban ", StringComparison.OrdinalIgnoreCase))
            {
                if (!IsOwner(userId)) { await message.Channel.SendMessageAsync("❌ **FORBIDDEN** — Owner only!"); return; }
                string[] p = cmd.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (p.Length < 2) { await message.Channel.SendMessageAsync("✅ Usage: `!unban @User`"); return; }
                if (!MentionUtils.TryParseUser(p[1], out ulong tid)) { await message.Channel.SendMessageAsync("❌ Invalid user"); return; }

                var guild = (message.Channel as IGuildChannel)?.Guild;
                if (guild == null) return;
                await guild.RemoveBanAsync(tid);
                await message.Channel.SendMessageAsync($"✅ **UNBANNED!** <@{tid}> can join again!");
                return;
            }

            // ==========================================
            // 👑 OWNER — ECONOMY
            // ==========================================

            // !give
            if (cmd.StartsWith("!give ", StringComparison.OrdinalIgnoreCase))
            {
                if (!IsOwner(userId)) { await message.Channel.SendMessageAsync("❌ **FORBIDDEN** — Owner only!"); return; }
                string[] p = cmd.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (p.Length < 3) { await message.Channel.SendMessageAsync("✅ Usage: `!give @User <amount>`"); return; }
                if (!MentionUtils.TryParseUser(p[1], out ulong tid)) { await message.Channel.SendMessageAsync("❌ Invalid user"); return; }
                if (!long.TryParse(p[2], out long amt) || amt <= 0) { await message.Channel.SendMessageAsync("❌ Invalid amount"); return; }

                EnsureAccount(tid);
                _balances[tid] += amt;
                SaveData();
                var targetUser = _client.GetUser(tid);
                await message.Channel.SendMessageAsync($"👑 **COINS GIVEN!**\n💰 To: **{targetUser?.Username ?? "User"}**\n🪙 +**{amt:N0} Coins**");
                return;
            }

            // ==========================================
            // 🖥️ SYSTEM COMMANDS
            // ==========================================

            // !ping
            if (cmd == "!ping")
            {
                await message.Channel.SendMessageAsync($"🏓 Pong! Latency: **{_client.Latency}ms**");
                return;
            }

            // ==========================================
            // 📖 HELP — WITH MODERATION COMMANDS
            // ==========================================
            if (cmd == "!help")
            {
                var helpEmbed = new EmbedBuilder()
                    .WithTitle("🏆 VORTEX | JHER BOT")
                    .WithDescription("Welcome to **VORTEX** — Economy & Arcade Bot.\nEarn virtual Vortex Coins through free games and activities!")
                    .AddField("💰 ECONOMY",
                        "`!balance` / `!bal` — View your wallet\n" +
                        "`!daily` — Claim daily reward\n" +
                        "`!work` — Earn coins\n" +
                        "`!leaderboard` / `!lb` — View rankings")
                    .AddField("🎮 ARCADE GAMES",
                        "`!coinflip heads` — Coinflip game\n" +
                        "`!coinflip tails` — Coinflip game\n" +
                        "`!dice` — Roll the dice\n" +
                        "`!rps rock/paper/scissors` — Rock Paper Scissors\n" +
                        "`!guess 5` — Guess a number\n" +
                        "`!slots` — Free arcade slots")
                    .AddField("🏆 CHALLENGES",
                        "`!trivia` — Answer trivia questions\n" +
                        "`!answer a/b/c` — Submit answer\n" +
                        "`!reaction` — Fast reaction challenge\n" +
                        "`!arcade` — View arcade games")
                    .AddField("👑 OWNER / MODERATION",
                        "`!give @User <amount>` — Give virtual coins\n" +
                        "`!kick @User [Reason]` — Kick user\n" +
                        "`!jail @User [minutes]` — Jail user\n" +
                        "`!unjail @User` — Release from jail\n" +
                        "`!ban @User [Reason]` — Ban user\n" +
                        "`!unban @User` — Unban user")
                    .AddField("HEADTEXT GRADIENT",
                   
                        "`!gradient <Text> <StartColor> <EndColor>` — 🎨 Generate gradient text\n")
                        
                    .AddField("ℹ️ INFORMATION",
                        "Vortex Coins are virtual server points only.\nThey have no real-world monetary value.\n\n🔧 **VORTEX • Economy & Entertainment**")
                    .WithColor(98, 51, 255)
                    .Build();

                await message.Channel.SendMessageAsync(embed: helpEmbed);
                return;
            }
        }

        private Task Log(LogMessage msg)
        {
            Console.WriteLine(msg.ToString());
            return Task.CompletedTask;
        }
    }
}
