namespace GdeltSearchUI;

internal static class BlueskyStarterPackCreator
{
    private const string PackName        = "GDELT News & Data Bots";
    private const string PackDescription =
        "Automated Bluesky bots tracking US gas prices, earthquakes, national debt, " +
        "energy futures, gun violence, Congress, NASA astronomy pictures, stock markets, " +
        "and severe weather alerts.";

    public static async Task CreateAsync(CancellationToken ct = default)
    {
        var accounts = CredentialManager.LoadAllBlueskyAccounts();
        if (accounts.Count == 0)
        {
            Console.WriteLine("ERROR: No bot accounts configured.");
            return;
        }

        Console.WriteLine($"Creating a starter pack for each of {accounts.Count} account(s).");
        Console.WriteLine();

        using var client = new BlueskyFollowClient();

        // Phase 1: resolve all member DIDs using the first account that authenticates.
        var members = await ResolveMemberDidsAsync(client, accounts, ct);
        if (members is null) return;

        // Phase 2: create a starter pack owned by each account.
        foreach (var (label, _, handle, password) in accounts)
        {
            Console.WriteLine($"══════════════════════════════════════════════════════");
            Console.WriteLine($"  Owner: @{handle} ({label})");
            Console.WriteLine($"══════════════════════════════════════════════════════");

            Console.Write("  Authenticating... ");
            var (ownerDid, jwt, authError) = await client.AuthenticateAsync(handle, password, ct);
            if (authError is not null)
            {
                Console.WriteLine($"FAILED — {authError}");
                Console.WriteLine();
                continue;
            }
            Console.WriteLine("OK");

            await CreatePackForAccountAsync(client, ownerDid, handle, jwt, members, ct);
            Console.WriteLine();

            await Task.Delay(1000, ct);
        }

        Console.WriteLine("Done.");
    }

    private static async Task<List<(string Label, string Handle, string Did)>?> ResolveMemberDidsAsync(
        BlueskyFollowClient client,
        IReadOnlyList<(string Label, string Slug, string Handle, string Password)> accounts,
        CancellationToken ct)
    {
        // Auth as the first account just long enough to resolve DIDs.
        var (_, firstHandle, _, firstPassword) = accounts[0];
        Console.Write($"Authenticating as @{firstHandle} to resolve DIDs... ");
        var (_, jwt, authError) = await client.AuthenticateAsync(firstHandle, firstPassword, ct);
        if (authError is not null)
        {
            Console.WriteLine($"FAILED — {authError}");
            return null;
        }
        Console.WriteLine("OK");

        Console.WriteLine("Resolving account DIDs:");
        var members = new List<(string Label, string Handle, string Did)>();
        foreach (var (label, _, handle, _) in accounts)
        {
            Console.Write($"  {label} (@{handle})... ");
            var profile = await client.GetPublicProfileAsync(handle, ct, jwt);
            if (profile is null || string.IsNullOrEmpty(profile.Did))
            {
                Console.WriteLine("FAILED (skipped)");
                continue;
            }
            Console.WriteLine(profile.Did);
            members.Add((label, handle, profile.Did));
        }

        if (members.Count == 0)
        {
            Console.WriteLine("No members resolved — aborting.");
            return null;
        }

        Console.WriteLine();
        return members;
    }

    private static async Task CreatePackForAccountAsync(
        BlueskyFollowClient client,
        string ownerDid,
        string ownerHandle,
        string jwt,
        List<(string Label, string Handle, string Did)> members,
        CancellationToken ct)
    {
        // Create the list.
        Console.Write("  Creating list... ");
        string listUri;
        try { listUri = await client.CreateListAsync(ownerDid, jwt, PackName, PackDescription, ct); }
        catch (Exception ex) { Console.WriteLine($"FAILED — {ex.Message}"); return; }
        Console.WriteLine($"OK → {listUri}");

        // Add all members to the list.
        Console.WriteLine("  Adding members:");
        foreach (var (mLabel, mHandle, mDid) in members)
        {
            Console.Write($"    @{mHandle} ({mLabel})... ");
            try
            {
                await client.CreateListItemAsync(ownerDid, jwt, listUri, mDid, ct);
                Console.WriteLine("OK");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FAILED — {ex.Message}");
            }
            await Task.Delay(300, ct);
        }

        // Create the starter pack.
        Console.Write("  Creating starter pack... ");
        try
        {
            var (_, rkey) = await client.CreateStarterPackAsync(
                ownerDid, jwt, PackName, PackDescription, listUri, ct);
            Console.WriteLine("OK");
            Console.WriteLine($"  URL: https://bsky.app/starter-pack/{ownerHandle}/{rkey}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FAILED — {ex.Message}");
        }
    }
}
