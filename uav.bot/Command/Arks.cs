using System;
using System.Text;
using System.Threading.Tasks;
using Discord.Commands;
using uav.Attributes;
using uav.logic.Constants;
using uav.logic.Database;
using uav.logic.Extensions;
using uav.logic.Models;
using uav.logic.Service;

namespace uav.Command;

public class Arks : CommandBase
{
    private readonly Credits creditService = new Credits();
    private readonly DatabaseService databaseService = new DatabaseService();
    private readonly Ark arkService = new Ark();

    public (double items, GV newValue) ArkCalculate(double gv, double goalGv, double cash, double exponent)
    {
        var multiplier = (goalGv - (gv - cash)) / cash;

        var arks = Math.Ceiling(Math.Log(multiplier) / Math.Log(exponent));
        var newValue = cash * Math.Pow(exponent, arks) + (gv - cash);

        return (arks, GV.FromNumber(newValue));
    }

    private const double cashArkChance = .7d;
    private const double dmArkChance = 1 - cashArkChance;
    private const double arksPerHour = 10d;

    [Command("basecred")]
    [Summary("Tell UAV about the base credits you get for your current GV, or query the range allowed for that GV tier.")]
    [Usage("currentGV [baseCredits [gv2 credits2] ...] ")]
    public async Task BaseCredits(params string[] parameters)
    {
        GV gvValue;
        {
            string error = null;
            if (parameters.Length == 0 || !GV.TryFromString(parameters[0], out gvValue, out error))
            {
                await ReplyAsync($"Invalid input. Usage: `!basecred currentGV [baseCredits ...]`{(error != null ? $"\n{error}" : string.Empty)}");
                return;
            }
        }

        if (gvValue < 10_000_000 || gvValue > 1e109)
        {
            await ReplyAsync($"Invalid input. GV must be between 10M and 1E+109");
            return;
        }

        if (parameters.Length == 1)
        {
            var msg = await arkService.QueryCreditRange(gvValue);

            await ReplyAsync(msg);
            return;
        }

        if (parameters.Length % 2 != 0)
        {
            await ReplyAsync("When entering more than one set of credits, you have to provide an even number of parameters as GV and base-credit pairs.");
            return;
        }

        var messages = parameters.NAtATime(2)
            .SelectAsync(a => arkService.UpdateCredits(a[0], a[1], Context.User.ToDbUser()));   
        var sb = new StringBuilder();
        var savedAny = false;
        await foreach (var msg in messages)
        {
            sb.AppendLine(msg.message);
            savedAny = savedAny || msg.success;
        }

        if (sb.Length > 0)
        {
            if (savedAny)
            {
                var contributionCount = await databaseService.CountByUser(Context.User.ToDbUser());
                if (contributionCount.total == 1)
                {
                    sb.AppendLine($"Thank you for your very first contribution!");
                }
                else
                {
                    sb.AppendLine($"You have now contributed **{contributionCount.total}** data point(s), **{contributionCount.distinctBaseCredits}** different base credits, across **{contributionCount.distinctTiers}** tiers.");
                }                    
            }

            await ReplyAsync(sb.ToString());
            return;
        }
    }
}
