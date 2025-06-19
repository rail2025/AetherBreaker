using System;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using AetherBreaker.Networking;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using ImGuiNET;

namespace AetherBreaker.Windows
{
    public class MultiplayerWindow : Window, IDisposable
    {
        private readonly Plugin plugin;

        private enum SessionState { Choice, PassphraseEntry, Loading }
        private SessionState currentState = SessionState.Choice;

        private string serverAddress = "wss://aetherbreaker-server.onrender.com/ws"; // Placeholder Server
        private string inputPassphrase = "";
        private string generatedPassphrase = "";
        private string statusMessage = "Disconnected";

        // Passphrase generation words
        private static readonly Random Random = new();
        private static readonly string[] OpinionVerbs = { "I like", "I hate", "I want", "I need", "Craving", "Seeking", "Avoiding", "Serving", "Finding", "Cooking", "Tasting", "I found", "I lost", "I traded", "He stole", "She sold", "They want", "Remembering", "Forgetting", "Questioning", "Analyzing", "Ignoring", "Praising", "Chasing", "Selling" };
        private static readonly string[] Adjectives = { "spicy", "creamy", "sultry", "glimmering", "ancient", "crispy", "zesty", "hearty", "fluffy", "savory", "frozen", "bubbling", "forbidden", "radiant", "somber", "dented", "gilded", "rusted", "glowing", "cracked", "smelly", "aromatic", "stale", "fresh", "bitter", "sweet", "silken", "spiky" };
        private static readonly string[] FfxivNouns = { "Miqote", "Lalafell", "Gridanian", "Ul'dahn", "Limsan", "Ishgardian", "Doman", "Hrothgar", "Viera", "Garlean", "Sharlayan", "Sylph", "Au Ra", "Roegadyn", "Elezen", "Thavnairian", "Coerthan", "Ala Mhigan", "Ronkan", "Eorzean", "Astrologian", "Machinist", "Samurai", "Dancer", "Paladin", "Warrior" };
        private static readonly string[] FoodItems = { "rolanberry pie", "LaNoscean toast", "dodo omelette", "pixieberry tea", "king salmon", "knightly bread", "stone soup", "archon burgers", "bubble chocolate", "tuna miq", "syrcus tower", "dalamud shard", "aetheryte shard", "allagan tomestone", "company seal", "gil-turtle", "cactuar needle", "malboro breath", "behemoth horn", "mandragora root", "black truffle", "popoto", "ruby tomato", "apkallu egg", "thavnairian onion" };
        private static readonly string[] ActionPhrases = { "in my inventory", "on the marketboard", "from a retainer", "for the Grand Company", "in a treasure chest", "from a guildhest", "at the Gold Saucer", "near the aetheryte", "without permission", "for a friend", "under the table", "with great haste", "against all odds", "for my free company", "in the goblet" };

        public MultiplayerWindow(Plugin plugin) : base("AetherBreaker Multiplayer", ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse)
        {
            this.plugin = plugin;
        }

        public void Dispose()
        {
            // Unsubscribe from events here if you were to subscribe
        }

        public override void OnOpen()
        {
            this.currentState = SessionState.Choice;
            this.statusMessage = plugin.NetworkManager.IsConnected ? "Connected" : "Disconnected";
            this.inputPassphrase = "";
            this.generatedPassphrase = "";
        }

        public void SetConnectionStatus(string status, bool isError)
        {
            this.statusMessage = status;
            if (isError)
            {
                this.currentState = SessionState.Choice; // Go back to choice on error
            }
            else if (status == "Connected")
            {
                this.IsOpen = false; // Close window on successful connection
            }
        }

        public override void Draw()
        {
            var viewportSize = ImGui.GetMainViewport().Size;
            ImGui.SetNextWindowPos(viewportSize / 2, ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));

            switch (currentState)
            {
                case SessionState.Choice: DrawChoiceView(); break;
                case SessionState.PassphraseEntry: DrawPassphraseEntryView(); break;
                case SessionState.Loading: DrawLoadingView(); break;
            }
        }

        private void DrawLoadingView()
        {
            ImGui.Text(statusMessage);
            ImGui.Spacing();
            if (ImGui.Button("Cancel"))
            {
                _ = plugin.NetworkManager.DisconnectAsync();
                currentState = SessionState.Choice;
            }
        }

        private void DrawChoiceView()
        {
            ImGui.Text("Choose Connection Method");
            ImGui.Separator();
            ImGui.Spacing();

            var paneWidth = 250 * ImGuiHelpers.GlobalScale;

            using (ImRaii.Group())
            {
                ImGui.TextWrapped("Quick Sync (Party)");
                ImGui.Separator();
                ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + paneWidth - 10);
                ImGui.TextWrapped("Creates a secure room for your current party. All party members must click this to join the same room.");
                ImGui.PopTextWrapPos();
                ImGui.Spacing();

                bool inParty = Plugin.PartyList != null && Plugin.PartyList.Length > 0;
                using (ImRaii.Disabled(!inParty))
                {
                    if (ImGui.Button("Quick Sync##QuickSyncButton", new Vector2(paneWidth, 0)))
                    {
                        string partyPassphrase = GetPartyIdHash();
                        if (string.IsNullOrEmpty(partyPassphrase))
                        {
                            statusMessage = "Could not get Party ID. Are you in a party?";
                        }
                        else
                        {
                            statusMessage = "Connecting via Party ID...";
                            currentState = SessionState.Loading;
                            _ = plugin.NetworkManager.ConnectAsync(serverAddress, partyPassphrase);
                        }
                    }
                }
                if (!inParty && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                {
                    ImGui.SetTooltip("You must be in a party to use this option.");
                }
            }

            ImGui.SameLine(0, 15f * ImGuiHelpers.GlobalScale);

            using (ImRaii.Group())
            {
                ImGui.TextWrapped("Passphrase Connect");
                ImGui.Separator();
                ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + paneWidth - 10);
                ImGui.TextWrapped("Create or join a session using a shared passphrase. Good for cross-world groups.");
                ImGui.PopTextWrapPos();
                ImGui.Spacing();
                if (ImGui.Button("Use Passphrase##PassphraseButton", new Vector2(paneWidth, 0)))
                {
                    currentState = SessionState.PassphraseEntry;
                }
            }

            ImGui.Spacing();
            ImGui.Separator();
            if (!string.IsNullOrEmpty(statusMessage) && statusMessage != "Disconnected" && statusMessage != "Connected")
            {
                ImGui.TextColored(new Vector4(1f, 0.3f, 0.3f, 1f), statusMessage);
            }
            if (ImGui.Button("Cancel", new Vector2(120, 0))) this.IsOpen = false;
        }

        private void DrawPassphraseEntryView()
        {
            ImGui.Text("Connect with Passphrase");
            ImGui.Separator();

            ImGui.InputText("Server Address", ref serverAddress, 256);
            ImGui.Spacing();

            ImGui.Text("Create new & copy:");
            if (ImGui.Button("Generate"))
            {
                string opinion = OpinionVerbs[Random.Next(OpinionVerbs.Length)];
                string adjective = Adjectives[Random.Next(Adjectives.Length)];
                string noun = FfxivNouns[Random.Next(FfxivNouns.Length)];
                string food = FoodItems[Random.Next(FoodItems.Length)];
                string action = ActionPhrases[Random.Next(ActionPhrases.Length)];
                generatedPassphrase = $"{opinion} {adjective} {noun} {food} {action}.";
                inputPassphrase = generatedPassphrase;
                ImGui.SetClipboardText(generatedPassphrase);
            }
            ImGui.SameLine();
            ImGui.InputText("##GeneratedPassphrase", ref generatedPassphrase, 256, ImGuiInputTextFlags.ReadOnly);

            ImGui.Spacing();
            ImGui.Text("Join existing:");
            ImGui.InputText("Enter Passphrase", ref inputPassphrase, 256);

            ImGui.Separator();

            bool canConnect = !string.IsNullOrWhiteSpace(serverAddress) && !string.IsNullOrWhiteSpace(inputPassphrase);
            using (ImRaii.Disabled(!canConnect))
            {
                if (ImGui.Button("Connect"))
                {
                    statusMessage = $"Connecting with passphrase...";
                    currentState = SessionState.Loading;
                    _ = plugin.NetworkManager.ConnectAsync(serverAddress, inputPassphrase);
                }
            }
            ImGui.SameLine();
            if (ImGui.Button("Back")) currentState = SessionState.Choice;
        }

        private string GetPartyIdHash()
        {
            if (Plugin.PartyList == null || Plugin.PartyList.Length == 0)
                return "";

            var contentIds = Plugin.PartyList.Select(p => p.ContentId).ToList();
            contentIds.Sort();
            var combinedIdString = string.Join(",", contentIds);

            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(combinedIdString));
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }
                return builder.ToString();
            }
        }
    }
}
