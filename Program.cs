using Discord;
using Discord.WebSocket;
using Discord.Net;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Linq;
using System.Net;

namespace VortexBot
{
    class Program
    {
        private DiscordSocketClient _client;

        private const ulong OWNER_ID = 1432638241177075827;
        private const string DATA_FILE = "vortex_data.json";
        private const long NEW_USER_BALANCE = 1000000;
        private const int COMMAND_COOLDOWN_SECONDS = 20;
        private const int PORT = 10000;

        private Dictionary<ulong, long> _balances = new();
        private Dictionary<ulong, DateTime> _dailyCooldown = new();
        private Dictionary<ulong, DateTime> _workCooldown = new();
        private Dictionary<ulong, DateTime> _jailedUntil = new();
        private Dictionary<ulong, DateTime> _userCooldown = new();

        static void Main(string[] args)
        {
            new Program().MainAsync().GetAwaiter().GetResult();
        }

        public async Task MainAsync()
        {
            LoadData();
            StartDummyWebServer();

            var config = new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent
            };

            _client = new DiscordSocketClient(config);

            string token = Environment.GetEnvironmentVariable("DISCORD_TOKEN");
            if (string.IsNullOrWhiteSpace(token))
            {
                Console.WriteLine("❌ DISCORD_TOKEN environment variable not set!");
                return;
            }

            _client.Log += LogAsync;
            _client.MessageReceived += MessageReceivedAsync;
            _client.Ready += ReadyAsync;

            try
            {
                await _client.LoginAsync(TokenType.Bot, token);
                await _client.StartAsync();
            }
            catch (HttpException ex)
            {
                Console.WriteLine($"❌ Login Error: {ex.Message}");
                return;
            }

            await Task.Delay(-1);
        }

        private void StartDummyWebServer()
        {
            try
            {
                var listener = new HttpListener();
                listener.Prefixes.Add($"http://+:{PORT}/");
                listener.Start();
                Console.WriteLine($"✅ Web server listening on port {PORT}");

                Task.Run(async () =>
                {
                    while (listener.IsListening)
                    {
                        try
                        {
                            var ctx = await listener.GetContextAsync();
                            var response = ctx.Response;
                            string text = "Vortex Bot is running!";
                            byte[] buffer = System.Text.Encoding.UTF8.GetBytes(text);
                            response.ContentLength64 = buffer.Length;
                            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                            response.Close();
                        }
                        catch { }
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Port server warning: {ex.Message}");
            }
        }

        private Task LogAsync(LogMessage msg)
        {
            Console.WriteLine(msg.ToString());
            return Task.CompletedTask;
        }

        private Task ReadyAsync()
        {
            Console.WriteLine("✅ VORTEX BOT ONLINE & READY!");
            return Task.CompletedTask;
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

        private void EnsureUser(ulong userId)
        {
            if (!_balances.ContainsKey(userId))
                _balances[userId] = NEW_USER_BALANCE;
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
                    JailedUntil = _jailedUntil
                };
                string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(DATA_FILE, json);
            }
            catch (Exception ex) { Console.WriteLine($"Save Error: {ex.Message}"); }
        }

        private void LoadData()
        {
            try
            {
                if (!File.Exists(DATA_FILE)) return;
                string json = File.ReadAllText(DATA_FILE);
                var data = JsonSerializer.Deserialize<BotData>(json);
                if (data != null)
                {
                    _balances = data.Balances ?? new();
                    _dailyCooldown = data.DailyCooldown ?? new();
                    _workCooldown = data.WorkCooldown ?? new();
                    _jailedUntil = data.JailedUntil ?? new();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Load Error: {ex.Message}");
                _balances = new();
            }
        }

        private async Task MessageReceivedAsync(SocketMessage msg)
        {
            var userMsg = msg as SocketUserMessage;
            if (userMsg == null || userMsg.Content.Length < 2 || msg.Author.IsBot) return;

            ulong userId = msg.Author.Id;
            EnsureUser(userId);

            if (_jailedUntil.TryGetValue(userId, out var releaseTime) && DateTime.UtcNow < releaseTime)
                return;

            string[] args = userMsg.Content.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string cmdName = args.Length > 0 ? args[0].ToLower() : "";

            if (!CheckCooldown(userId, out TimeSpan cooldownRem))
            {
                await msg.Channel.SendMessageAsync($"⏳ **Cooldown!** Maghintay ng **{(int)cooldownRem.TotalSeconds} segundo** bago makagamit ulit!");
                return;
            }

            if (cmdName == "!balance" || cmdName == "!bal")
            {
                await msg.Channel.SendMessageAsync($"💰 <@{userId}> Balance: **{_balances[userId]:N0} Coins**");
                return;
            }

            if (cmdName == "!daily")
            {
                if (_dailyCooldown.TryGetValue(userId, out var dailyLast) && (DateTime.UtcNow - dailyLast).TotalHours < 24)
                {
                    var timeRem = TimeSpan.FromHours(24) - (DateTime.UtcNow - dailyLast);
                    await msg.Channel.SendMessageAsync($"⏳ Bumalik ka pagkalipas ng **{timeRem.Hours} oras at {timeRem.Minutes} minuto**!");
                    return;
                }
                int reward = new Random().Next(500, 1001);
                _balances[userId] += reward;
                _dailyCooldown[userId] = DateTime.UtcNow;
                SaveData();
                await msg.Channel.SendMessageAsync($"🎁 Claimed! +**{reward:N0} Coins**! Balanse: **{_balances[userId]:N0}**");
                return;
            }

            if (cmdName == "!work")
            {
                if (_workCooldown.TryGetValue(userId, out var workLast) && (DateTime.UtcNow - workLast).TotalMinutes < 5)
                {
                    var workRem = TimeSpan.FromMinutes(5) - (DateTime.UtcNow - workLast);
                    await msg.Channel.SendMessageAsync($"⏳ Magpahinga ka muna! **{workRem.Seconds} segundo** na lang!");
                    return;
                }
                string[] jobs = { "Nag-code", "Nag-maintain ng server", "Nag-ayos ng makina", "Nagbantay ng perimeter", "Nag-calibrate ng sistema" };
                int earnings = new Random().Next(100, 401);
                _balances[userId] += earnings;
                _workCooldown[userId] = DateTime.UtcNow;
                SaveData();
                await msg.Channel.SendMessageAsync($"🛠️ {jobs[new Random().Next(jobs.Length)]} ka! +**{earnings:N0} Coins**! Balanse: **{_balances[userId]:N0}**");
                return;
            }

            if (cmdName == "!lb" || cmdName == "!leaderboard")
            {
                var top10 = _balances.OrderByDescending(x => x.Value).Take(10).ToList();
                string text = "🏆 **TOP 10 RICHEST**\n";
                int rank = 1;
                foreach (var entry in top10)
                {
                    string medal = rank switch { 1 => "🥇", 2 => "🥈", 3 => "🥉", _ => "🏅" };
                    text += $"{medal} #{rank}: <@{entry.Key}> — **{entry.Value:N0} Coins**\n";
                    rank++;
                }
                await msg.Channel.SendMessageAsync(text);
                return;
            }

            if (cmdName == "!slots")
            {
                long bet = args.Length >= 2 && long.TryParse(args[1], out long b) && b > 0 ? b : 0;
                if (bet > _balances[userId])
                {
                    await msg.Channel.SendMessageAsync("❌ Kulang ang pera mo!");
                    return;
                }

                string[] common = { "🍒", "🍒", "🍒", "🍒", "🍋", "🍋", "🍋", "🍋", "🍇", "🍇", "7️⃣", "7️⃣" };
                string star = "⭐";
                string diamond = "💎";

                string a, symB, c;
                double roll = new Random().NextDouble();

                // ✅ 40% TALO | 40% PANALO | 15% ⭐5x | 5% 💎50x
                if (roll < 0.40)
                {
                    // ❌ 40% CHANCE: TALO — WALANG KAPAREHAS
                    string[] loseSymbols = { "🍒", "🍋", "🍇", "7️⃣" };
                    do
                    {
                        a = loseSymbols[new Random().Next(loseSymbols.Length)];
                        symB = loseSymbols[new Random().Next(loseSymbols.Length)];
                        c = loseSymbols[new Random().Next(loseSymbols.Length)];
                    } while (a == symB || symB == c || a == c);
                }
                else if (roll < 0.80)
                {
                    // ✅ 40% CHANCE: PANALO — TRIPLE COMMON
                    int idx = new Random().Next(common.Length);
                    a = common[idx]; symB = common[idx]; c = common[idx];
                }
                else if (roll < 0.95)
                {
                    // ⭐ 15% CHANCE: TRIPLE STAR
                    a = star; symB = star; c = star;
                }
                else
                {
                    // 💎 5% CHANCE: TRIPLE DIAMOND
                    a = diamond; symB = diamond; c = diamond;
                }

                string resultText;
                long multiplier = 0;

                if (a == symB && symB == c)
                {
                    if (a == diamond) { multiplier = 50; resultText = "💎💎💎 **MEGA JACKPOT! 50x PANALO!** 💎💎💎"; }
                    else if (a == star) { multiplier = 5; resultText = "⭐⭐⭐ **BIG WIN! 5x PANALO!** ⭐⭐⭐"; }
                    else { multiplier = 2; resultText = $"🎉 **JACKPOT! 2x PANALO!** {a}{a}{a}"; }
                }
                else if (a == symB || symB == c || a == c)
                {
                    multiplier = 1; resultText = "✨ **PARES! BALIK ANG TAYA!** ✨";
                }
                else
                {
                    multiplier = 0; resultText = "😔 **WALA. SUBOK ULIT!** 😔";
                }

                long winAmt = bet * multiplier;
                _balances[userId] = _balances[userId] - bet + winAmt;
                SaveData();

                await msg.Channel.SendMessageAsync($"```\n🎰 [ {a} ] [ {symB} ] [ {c} ]\n```\n{resultText}\n✅ Kita: **{winAmt:N0} Coins** | Balanse: **{_balances[userId]:N0}**");
                return;
            }

            if (IsOwner(userId))
            {
                if (cmdName == "!give" && args.Length >= 3 && ulong.TryParse(args[1].Replace("<@!", "").Replace(">", "").Replace("!", ""), out ulong targetId) && long.TryParse(args[2], out long giveAmt))
                {
                    EnsureUser(targetId);
                    _balances[targetId] += giveAmt;
                    SaveData();
                    await msg.Channel.SendMessageAsync($"👑 Ibinigay ang **{giveAmt:N0} Coins** kay <@{targetId}>!");
                    return;
                }

                if (cmdName == "!jail" && args.Length >= 3 && ulong.TryParse(args[1].Replace("<@!", "").Replace(">", "").Replace("!", ""), out ulong jailId) && int.TryParse(args[2], out int min))
                {
                    _jailedUntil[jailId] = DateTime.UtcNow.AddMinutes(min);
                    await msg.Channel.SendMessageAsync($"⛓️ <@{jailId}> ay nakulong ng **{min} minuto**!");
                    return;
                }
            }

            if (cmdName == "!help")
            {
                await msg.Channel.SendMessageAsync(@"
🏠 **VORTEX BOT COMMANDS**
💰 `!bal / !balance` — Tingnan ang balanse
🎁 `!daily` — Kunin ang pang-araw-araw na premyo
🛠️ `!work` — Magtrabaho at kumita
🎰 `!slots [halaga]` — Subukan ang suwerte
🏆 `!lb / !leaderboard` — Tingnan ang mayayaman
⏳ Lahat ng command: 20 segundong paghihintay
🎰 Slots: ❌40% Talo | ✅40% Panalo | ⭐15% 5x | 💎5% 50x
");
                return;
            }
        }
    }

    public class BotData
    {
        public Dictionary<ulong, long> Balances { get; set; } = new();
        public Dictionary<ulong, DateTime> DailyCooldown { get; set; } = new();
        public Dictionary<ulong, DateTime> WorkCooldown { get; set; } = new();
        public Dictionary<ulong, DateTime> JailedUntil { get; set; } = new();
    }
}
