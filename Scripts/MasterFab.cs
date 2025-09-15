using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine.Events;

public partial class MasterFab : MonoBehaviour
{
    public static MasterFab masterFab { get; private set; }

    [Header("Currency")]
    [Tooltip("The codes for using currencies in-game.")]
    public List<string> currencyCodes = new List<string>();
    [Space]
    [Header("News")]
    [Tooltip("Enables/Disables NewsData.")]
    public bool enableNews;
    [Space]
    [Header("Events")]
    [Space(10)]
    [Tooltip("Involkes when login is successful.")]
    public UnityEvent OnLoginSuccess;
    [Tooltip("Involkes when the user is banned.")]
    public UnityEvent IfBanned;
    [Space]
    [Header("Other")]
    [Space(10)]
    [Tooltip("If enabled, you will login on game start.")]
    public bool LoginOnAwake = true;

    private string playerName;

    private string playFabID;
    private Dictionary<string, int> currencyAmounts = new Dictionary<string, int>();
    private List<FriendInfo> friends = new List<FriendInfo>();
    private string dateText;
    private string titleText;
    private string bodyText;

    public void Awake()
    {
        if (masterFab == null)
        {
            masterFab = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void Start()
    {
        if (LoginOnAwake)
        {
            RequestLogin();
        }
    }

    /// <summary>
    /// Logs in the user with the deviceUniqueIdentifier as your ID.
    /// </summary>
    public static void RequestLogin()
    {
        Debug.Log("Login Requested.");

        var request = new LoginWithCustomIDRequest
        {
            CustomId = SystemInfo.deviceUniqueIdentifier,
            CreateAccount = true,
            InfoRequestParameters = new GetPlayerCombinedInfoRequestParams
            {
                GetPlayerProfile = true
            }
        };
        PlayFabClientAPI.LoginWithCustomID(request, masterFab.LoginSucceed, masterFab.OnError);
    }

    void LoginSucceed(LoginResult result)
    {
        Debug.Log($"Login has succeeded.");

        GetAccountInfoRequest InfoRequest = new GetAccountInfoRequest();
        PlayFabClientAPI.GetAccountInfo(InfoRequest, AccountInfoSuccess, OnError);
        PlayFabClientAPI.GetUserInventory(new GetUserInventoryRequest(), UserInventorySuccess, OnError);

        RefreshFriendsList();

        if (enableNews)
        {
            ReadNews();
        }

        OnLoginSuccess?.Invoke();
    }

    void AccountInfoSuccess(GetAccountInfoResult result)
    {
        playFabID = result.AccountInfo.PlayFabId;

        PlayFabClientAPI.GetPlayerProfile(new GetPlayerProfileRequest { PlayFabId = playFabID }, profileResult =>
        {
            playerName = profileResult.PlayerProfile.DisplayName;

            if (string.IsNullOrEmpty(playerName))
            {
                string randomName = "User" + UnityEngine.Random.Range(1000, 9999);

                ChangeDisplayName(randomName);
            }

        }, OnError);
    }

    void UserInventorySuccess(GetUserInventoryResult result)
    {
        foreach (string code in currencyCodes)
        {
            if (result.VirtualCurrency.ContainsKey(code))
            {
                currencyAmounts[code] = result.VirtualCurrency[code];
            }
            else
            {
                Debug.LogWarning($"Currency code '{code}' not found in inventory.");
                currencyAmounts[code] = 0;
            }
        }
    }

    void GetPlayerProfileSuccess(GetPlayerProfileResult result)
    {
        playerName = result.PlayerProfile.DisplayName;
    }

    /// <summary>
    /// Changes your PlayFab display name.
    /// </summary>
    public static void ChangeDisplayName(string newName)
    {
        var searchRequest = new GetPlayerProfileRequest
        {
            PlayFabId = masterFab.playFabID
        };

        PlayFabClientAPI.GetPlayerProfile(searchRequest, profileResult =>
        {
            if (profileResult.PlayerProfile.DisplayName == newName)
            {
                Debug.LogWarning($"Username is already in use.");
                return;
            }

            var updateRequest = new UpdateUserTitleDisplayNameRequest
            {
                DisplayName = newName
            };

            PlayFabClientAPI.UpdateUserTitleDisplayName(updateRequest, updateResult =>
            {
                masterFab.playerName = updateResult.DisplayName;
                Debug.Log($"Username successfully changed to {masterFab.playerName}.");
            }, masterFab.OnError);

        }, masterFab.OnError);
    }

    /// <summary>
    /// Adds a certain amount of currency to the user.
    /// </summary>
    public static void AddCurrency(string currencyCode, int amount)
    {
        var request = new AddUserVirtualCurrencyRequest
        {
            VirtualCurrency = currencyCode,
            Amount = amount
        };

        PlayFabClientAPI.AddUserVirtualCurrency(request, result =>
        {
            if (!masterFab.currencyAmounts.ContainsKey(currencyCode))
                masterFab.currencyAmounts[currencyCode] = 0;

            masterFab.currencyAmounts[currencyCode] += amount;

            Debug.Log($"{GetCurrencyAmount(currencyCode)} {currencyCode} added to users account.");
        }, masterFab.OnError);
    }

    /// <summary>
    /// Purchases an item for a certain price.
    /// </summary>
    public static void BuyItem(string itemId, string currencyCode, int price)
    {
        var request = new PurchaseItemRequest
        {
            ItemId = itemId,
            Price = price,
            VirtualCurrency = currencyCode
        };

        PlayFabClientAPI.PurchaseItem(request, result =>
        {
            Debug.Log($"{itemId} has been purchased.");
        }, masterFab.OnError);
    }

    /// <summary>
    /// Writes a key with a value to user data.
    /// </summary>
    public static void WriteValue(string key, string value)
    {
        var request = new UpdateUserDataRequest
        {
            Data = new Dictionary<string, string>
            {
                { key, value }
            }
        };
        PlayFabClientAPI.UpdateUserData(request, result =>
        {
            Debug.Log($"Value saved to PlayFab: {key} = {value}");
        }, masterFab.OnError);
    }

    /// <summary>
    /// Reads a value from a key in user data.
    /// </summary>
    public static void ReadValue(string key, Action<string> onSuccess)
    {

        PlayFabClientAPI.GetUserData(new GetUserDataRequest(), result =>
        {
            if (result.Data != null && result.Data.ContainsKey(key))
            {
                string value = result.Data[key].Value;
                Debug.Log($"Loaded {key}: {value}");
                onSuccess?.Invoke(value);
            }
            else
            {
                Debug.LogWarning($"Key '{key}' not found in PlayFab user data.");
                onSuccess?.Invoke(null);
            }
        }, masterFab.OnError);
    }

    /// <summary>
    /// Gets the users account ID.
    /// </summary>
    public static string GetAccountID()
    {
        return masterFab.playFabID;
    }

    /// <summary>
    /// Gets the amount of a certain currency a user has.
    /// </summary>
    public static int? GetCurrencyAmount(string currencyCode)
    {
        if (masterFab.currencyAmounts.TryGetValue(currencyCode, out int value))
        {
            return value;
        }

        Debug.LogWarning($"Currency '{currencyCode}' is invalid.");
        return null;
    }

    /// <summary>
    /// Gets the users display name.
    /// </summary>
    public static string GetDisplayName()
    {
        return masterFab.playerName;
    }

    /// <summary>
    /// Gets the titles news.
    /// </summary>
    public static void ReadNews()
    {
        PlayFabClientAPI.GetTitleNews(new GetTitleNewsRequest(), result =>
        {
            if (result.News == null || result.News.Count == 0)
            {
                Debug.Log("No news available.");
                return;
            }

            Debug.Log("News recieved.");
            foreach (var newsResult in result.News)
            {
                masterFab.dateText = newsResult.Timestamp.ToString();
                masterFab.titleText = newsResult.Title;
                masterFab.bodyText = newsResult.Body;
            }
        }, masterFab.OnError);
    }

    /// <summary>
    /// Gets the current news date.
    /// </summary>
    public string GetNewsDate()
    {
        return dateText;
    }

    /// <summary>
    /// Gets the current news title.
    /// </summary>
    public string GetNewsTitle()
    {
        return titleText;
    }

    /// <summary>
    /// Gets the current news body.
    /// </summary>
    public string GetNewsBody()
    {
        return bodyText;
    }

    /// <summary>
    /// Adds a friend by PlayFabID.
    /// </summary>
    public static void AddFriend(string PlayFabID)
    {
        var request = new AddFriendRequest
        {
            FriendPlayFabId = PlayFabID,
        };

        PlayFabClientAPI.AddFriend(request, result =>
        {
            Debug.Log($"Friend {PlayFabID} added.");
            masterFab.RefreshFriendsList();
        }, masterFab.OnError);
    }

    /// <summary>
    /// Removes a friend by PlayFabId.
    /// </summary>
    public static void RemoveFriend(string PlayFabID)
    {
        var request = new RemoveFriendRequest
        {
            FriendPlayFabId = PlayFabID
        };

        PlayFabClientAPI.RemoveFriend(request, result =>
        {
            Debug.Log($"Friend {PlayFabID} removed.");
            masterFab.RefreshFriendsList();
        }, masterFab.OnError);
    }

    /// <summary>
    /// Refreshes the local friends list.
    /// </summary>
    public void RefreshFriendsList()
    {
        var request = new GetFriendsListRequest();

        PlayFabClientAPI.GetFriendsList(request, result =>
        {
            friends = result.Friends ?? new List<FriendInfo>();
            Debug.Log($"Friends list updated.");
        }, OnError);
    }

    /// <summary>
    /// Gets the cached friends list.
    /// </summary>
    public static List<FriendInfo> GetFriends()
    {
        return new List<FriendInfo>(masterFab.friends);
    }

    private void OnError(PlayFabError error)
    {
        if (error.Error == PlayFab.PlayFabErrorCode.AccountBanned)
        {
            Debug.Log("Account is banned.");

            IfBanned?.Invoke();
        }
        else
        {
            Debug.LogError(error.GenerateErrorReport());
        }
    }
}

#if UNITY_EDITOR
public partial class MasterFab
{
    [UnityEditor.CustomEditor(typeof(MasterFab))]
    public class MasterFabGUI : UnityEditor.Editor
    {
        private void OnEnable() => UnityEditor.EditorApplication.update += RepaintInspector;
        private void OnDisable() => UnityEditor.EditorApplication.update -= RepaintInspector;
        private void RepaintInspector() => Repaint();

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            GUILayout.Space(20);
            GUILayout.BeginVertical("box");

            if (UnityEditor.EditorApplication.isPlaying)
            {
                if (PlayFab.PlayFabClientAPI.IsClientLoggedIn())
                {
                    GUILayout.Label($"Display Name: {MasterFab.GetDisplayName()}");
                    GUILayout.Label($"Account ID: {MasterFab.GetAccountID()}");

                    GUILayout.Space(10);
                    GUILayout.Label("Currencies:");
                    foreach (var kvp in MasterFab.masterFab.currencyCodes)
                    {
                        var amount = MasterFab.GetCurrencyAmount(kvp);
                        GUILayout.Label($"{kvp}: {amount}");
                    }

                    GUILayout.Space(10);
                    GUILayout.Label("Friends:");

                    var friends = MasterFab.GetFriends();
                    if (friends.Count == 0)
                    {
                        GUILayout.Label("No friends found.");
                    }
                    else
                    {
                        foreach (var f in friends)
                        {
                            GUILayout.BeginHorizontal("box");
                            GUILayout.Label($"{f.TitleDisplayName ?? "Unknown"}");
                            GUILayout.Label($"ID: {f.FriendPlayFabId}");
                            GUILayout.EndHorizontal();
                        }
                    }

                    if (GUILayout.Button("Refresh Friends List"))
                    {
                        MasterFab.masterFab.RefreshFriendsList();
                    }
                }
            }
            else
            {
                GUILayout.Label("Game is not running.");
            }

            GUILayout.EndVertical();
        }
    }
}
#endif