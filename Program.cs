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
        private const ulong OWNER_ID = 1432638241177075827;
        private const string DATA_FILE = "vortex_data.json";
        private const long NEW_PLAYER_BALANCE = 100000;
        private const int COMMAND_COOLDOWN_SECONDS = 20;

        private DiscordSocketClient _client = null!;
        private readonly Random _random = new();
        private readonly Dictionary<ulong, long> _balances = new();
        private readonly Dictionary<ulong, DateTime> _dailyCooldown = new();
        private readonly Dictionary<ulong, DateTime> _workCooldown = new();
        private readonly Dictionary<ulong, DateTime> _jailedUsers = new();
        private readonly Dictionary<ulong, DateTime> _userCooldown = new();

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

        private bool IsOwner(ulong userId) => userId == OWNER_ID;

        private bool CheckCooldown(ulong userId, out TimeSpan cooldownRem)
        {
            cooldownRem = TimeSpan.Zero;
            if (IsOwner(userId)) return true;
            if (_userCooldown.TryGetValue(userId, out var lastUsed))
            {
                cooldownRem = lastUsed.AddSeconds(COMMAND_COOLDOWN_SECONDS) - DateTime.UtcNow;
                if (cooldownRem.TotalSeconds > 0) return false;
            }
            _userCooldown[userId] = DateTime.UtcNow;
            return true;
        }

        private async Task MessageHandler(SocketMessage message)
        {
            if (message.Author.IsBot) return;
            string cmd = message.Content.Trim();
            if (string.IsNullOrWhiteSpace(cmd) || !cmd.StartsWith("!")) return;
            ulong userId = message.Author.Id;
            EnsureAccount(userId);

            if (!CheckCooldown(userId, out TimeSpan cooldownRem))
            {
                await message.Channel.SendMessageAsync($"⏳ **Cooldown!** Maghintay ng **{cooldownRem.Seconds} segundo** bago makagamit ulit!");
                return;
            }

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

            if (cmd == "!balance" || cmd == "!bal")
            {
                long bal = _balances[userId];
                await message.Channel.SendMessageAsync($"💰 Your Balance: **{bal:N0} Vortex Coins**");
                return;
            }

            if (cmd == "!daily")
            {
                var now = DateTime.UtcNow;
                if (_dailyCooldown.TryGetValue(userId, out var dailyLast) && (now - dailyLast).TotalHours < 24)
                {
                    var timeRem = TimeSpan.FromHours(24) - (now - dailyLast);
                    await message.Channel.SendMessageAsync($"⏳ Come back in **{timeRem.Hours}h {timeRem.Minutes}m**");
                    return;
                }
                int reward = _random.Next(500, 1001);
                _balances[userId] += reward;
                _dailyCooldown[userId] = now;
                SaveData();
                await message.Channel.SendMessageAsync($"🎁 Claimed! +**{reward:N0} Coins!**");
                return;
            }

            if (cmd == "!work")
            {
                var now = DateTime.UtcNow;
                if (_workCooldown.TryGetValue(userId, out var workLast) && (now - workLast).TotalMinutes < 5)
                {
                    var workRem = TimeSpan.FromMinutes(5) - (now - workLast);
                    await message.Channel.SendMessageAsync($"⏳ Cooldown: **{workRem.Seconds}s**");
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

            if (cmd.StartsWith("!coinflip ", StringComparison.OrdinalIgnoreCase))
            {
                string[] parts = cmd.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 3)
                {
                    await message.Channel.SendMessageAsync("⚠️ Usage: `!coinflip heads 100` or `!coinflip tails 500`");
                    return;
                }
                string choice = parts[1].ToLower();
                if (choice != "heads" && choice != "tails")
                {
                    await message.Channel.SendMessageAsync("⚠️ Please choose either **heads** or **tails**!");
                    return;
                }
                if (!long.TryParse(parts[2], out long betAmount) || betAmount <= 0)
                {
                    await message.Channel.SendMessageAsync("⚠️ Please enter a valid bet amount! Example: `!coinflip heads 100`");
                    return;
                }
                long currentBalance = _balances[userId];
                if (betAmount > currentBalance)
                {
                    await message.Channel.SendMessageAsync($"❌ Insufficient balance! You only have **{currentBalance:N0}** coins.");
                    return;
                }
                string result = _random.Next(2) == 0 ? "heads" : "tails";
                bool won = (choice == result);
                if (won)
                {
                    _balances[userId] += betAmount;
                    SaveData();
                    await message.Channel.SendMessageAsync($"🎉 **YOU WON!** The coin landed on **{result.ToUpper()}**!\n✅ You gained **{betAmount:N0}** coins!");
                }
                else
                {
                    _balances[userId] -= betAmount;
                    SaveData();
                    await message.Channel.SendMessageAsync($"😞 **YOU LOST!** The coin landed on **{result.ToUpper()}**!\n❌ You lost **{betAmount:N0}** coins.");
                }
                return;
            }

            if (cmd == "!dice")
            {
                int roll = _random.Next(1, 7);
                await message.Channel.SendMessageAsync($"🎲 Rolled: **{roll}**");
                return;
            }

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
                string outcome;
                if (player == bot) outcome = "🤝 **IT'S A TIE!**";
                else if ((player == "rock" && bot == "scissors") || (player == "paper" && bot == "rock") || (player == "scissors" && bot == "paper"))
                    outcome = "🎉 **YOU WIN!**";
                else outcome = "😔 **YOU LOST!**";
                await message.Channel.SendMessageAsync($"{emojiP} You: **{player.ToUpper()}** vs {emojiB} Bot: **{bot.ToUpper()}**\n{outcome}");
                return;
            }

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

            if (cmd.StartsWith("!slots", StringComparison.OrdinalIgnoreCase))
            {
                string[] p = cmd.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                long betAmount = 0;
                bool hasBet = false;

                if (p.Length >= 2 && long.TryParse(p[1], out long amt) && amt > 0)
                {
                    betAmount = amt;
                    hasBet = true;
                    long currentBalance = _balances[userId];
                    if (betAmount > currentBalance)
                    {
                        await message.Channel.SendMessageAsync($"❌ Insufficient balance! You only have **{currentBalance:N0}** coins.");
                        return;
                    }
                }

                string[] common = { "🍒", "🍒", "🍒", "🍒", "🍋", "🍋", "🍋", "🍋", "🍇", "🍇", "7️⃣", "7️⃣" };
                string rare5x = "⭐";
                string jackpot50x = "💎";

                string a, b, c;

                if (_random.NextDouble() < 0.85)
                {
                    a = common[_random.Next(common.Length)];
                    b = common[_random.Next(common.Length)];
                    c = common[_random.Next(common.Length)];
                }
                else if (_random.NextDouble() < 0.97)
                {
                    string[] mixed = { "🍒", "🍋", "🍇", "7️⃣", rare5x };
                    a = mixed[_random.Next(mixed.Length)];
                    b = mixed[_random.Next(mixed.Length)];
                    c = mixed[_random.Next(mixed.Length)];
                }
                else
                {
                    string[] mixed = { "🍒", "🍋", "🍇", "7️⃣", rare5x, jackpot50x };
                    a = mixed[_random.Next(mixed.Length)];
                    b = mixed[_random.Next(mixed.Length)];
                    c = mixed[_random.Next(mixed.Length)];
                }

                string resultText;
                long winMultiplier = 0;

                if (a == b && b == c)
                {
                    if (a == jackpot50x)
                    {
                        winMultiplier = 50;
                        resultText = "💎💎💎 **MEGA JACKPOT! 50x YOUR BET!** 💎💎💎";
                    }
                    else if (a == rare5x)
                    {
                        winMultiplier = 5;
                        resultText = "⭐⭐⭐ **BIG WIN! 5x YOUR BET!** ⭐⭐⭐";
                    }
                    else
                    {
                        winMultiplier = 2;
                        resultText = $"🎉 **JACKPOT! 2x YOUR BET!** 🎉";
                    }
                }
                else if (a == b || b == c || a == c)
                {
                    winMultiplier = 1;
                    resultText = "✨ **2 MATCH! COINS RETURNED!** ✨";
                }
                else
                {
                    winMultiplier = 0;
                    resultText = "😔 **NO MATCH! BET LOST!** 😔";
                }

                if (hasBet)
                {
                    long winnings = (long)(betAmount * winMultiplier);
                    if (winnings > 0)
                    {
                        _balances[userId] += winnings;
                        resultText += $"\n✅ +**{winnings:N0} Coins!**";
                    }
                    else
                    {
                        _balances[userId] -= betAmount;
                        resultText += $"\n❌ -**{betAmount:N0} Coins!**";
                    }
                    SaveData();
                }
                else
                {
                    resultText = "🎰 **FREE PLAY — NO BET!**\n" + resultText;
                }

                await message.Channel.SendMessageAsync($"```\n🎰 [ {a} ] [ {b} ] [ {c} ]\n```{resultText}");
                return;
            }

            if (cmd == "!arcade")
            {
                await message.Channel.SendMessageAsync(
                    "🎮 **ARCADE GAMES**\n" +
                    "`!coinflip heads 100` — Bet on heads\n" +
                    "`!coinflip tails 500` — Bet on tails\n" +
                    "`!dice` — Roll the dice\n" +
                    "`!rps rock/paper/scissors` — Rock Paper Scissors\n" +
                    "`!guess 5` — Guess a number 1-10\n" +
                    "`!slots` — Free slots\n" +
                    "`!slots 100` — Bet 100 coins\n" +
                    "💎 3x Diamond = 50x | ⭐ 3x Star = 5x | Others 3x = 2x ✅ SO EASY!\n" +
                    "⏳ All commands: 20s cooldown"
                );
                return;
            }

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

            if (cmd.StartsWith("!jail ", StringComparison.OrdinalIgnoreCase))
            {
                if (!IsOwner(userId)) { await message.Channel.SendMessageAsync("❌ **FORBIDDEN** — Owner only!"); return; }
                string[] p = cmd.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (p.Length < 2) { await message.Channel.SendMessageAsync("✅ Usage: `!jail @User [minutes]`\nDefault: 60 min"); return; }
                if (!MentionUtils.TryParseUser(p[1], out ulong tid)) { await message.Channel.SendMessageAsync("❌ Invalid user"); return; }
                int minutes = p.Length >= 3 && int.TryParse(p[2], out int m) && m > 0 ? m : 60;
                _jailedUsers[tid] = DateTime.UtcNow.AddMinutes(minutes);
                SaveData();
                await message.Channel.SendMessageAsync($"⛓️ **JAILED!**\n👤 User: <@{tid}>\n⏳ {minutes} min");
                return;
            }

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

            if (cmd.StartsWith("!giveall ", StringComparison.OrdinalIgnoreCase))
            {
                if (!IsOwner(userId)) { await message.Channel.SendMessageAsync("❌ **FORBIDDEN** — Owner only!"); return; }
                string[] p = cmd.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (p.Length < 2) { await message.Channel.SendMessageAsync("✅ Usage: `!giveall <amount>`"); return; }
                if (!long.TryParse(p[1], out long amt) || amt <= 0) { await message.Channel.SendMessageAsync("❌ Invalid amount"); return; }

                int count = 0;
                foreach (var uid in _balances.Keys.ToList())
                {
                    _balances[uid] += amt;
                    count++;
                }
                SaveData();
                await message.Channel.SendMessageAsync($"👑 **GIVE ALL SUCCESS!**\n💰 +**{amt:N0} Coins** added to **{count} users!**");
                return;
            }

            if (cmd == "!ping")
            {
                await message.Channel.SendMessageAsync($"🏓 Pong! Latency: **{_client.Latency}ms**");
                return;
            }

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
                        "`!coinflip heads 100` — Bet on heads\n" +
                        "`!coinflip tails 500` — Bet on tails\n" +
                        "`!dice` — Roll the dice\n" +
                        "`!rps rock/paper/scissors` — Rock Paper Scissors\n" +
                        "`!guess 5` — Guess a number\n" +
                        "`!slots` — Free slots\n" +
                        "`!slots 100` — Bet coins on slots")
                    .AddField("🎰 SLOTS PRIZES (SO EASY NOW!)",
                        "`3x 🍒🍋🍇7️⃣` = **2x** your bet ✅ 85% CHANCE!\n" +
                        "`3x ⭐⭐⭐` = **5x** your bet ⭐\n" +
                        "`3x 💎💎💎` = **50x** your bet 💎 SUPER RARE!\n" +
                        "`2 match` = **Return bet**\n" +
                        "`No match` = **Lose bet**")
                    .AddField("⏳ COOLDOWN",
                        "**20 seconds** between commands for all players!\n" +
                        "👑 **Owner has NO cooldown!**")
                    .AddField("👑 OWNER COMMANDS",
                        "`!give @User <amount>` — Give coins\n" +
                        "`!giveall <amount>` — Give coins to ALL\n" +
                        "`!kick @User [Reason]` — Kick user\n" +
                        "`!jail @User [min]` — Jail user\n" +
                        "`!unjail @User` — Release\n" +
                        "`!ban @User [Reason]` — Ban user\n" +
                        "`!unban @User` — Unban user")
                    .AddField("🎨 GRADIENT",
                        "`!gradient <Text> <Start> <End>` — Gradient text\n")
                    .AddField("ℹ️ INFORMATION",
                        "Vortex Coins are virtual points only.\nNo real money value.\n\n🔧 **VORTEX • Economy & Entertainment**")
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
