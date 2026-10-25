using HistoryKit;
using Qud.API;
using System;
using System.Collections.Generic;
using XRL;
using XRL.Annals;
using XRL.World;

public class PlayerSecretsMod : IPart
{
    public override bool WantEvent(int ID, int cascade)
    {
        return ID == SecretVisibilityChangedEvent.ID;
    }

    private static Dictionary<string,int> _sultanPeriodTable = null;
    private static Dictionary<string,int> sultanPeriodTable
    {
        get
        {
            if (_sultanPeriodTable == null)
            {
                SortedList<long,string> sultanDeaths = new();
                foreach (HistoricEntity e in QudHistoryHelpers.GetSultans(The.Game.sultanHistory))
                {
                    sultanDeaths.Add(e.lastYear, e.id);
                }
                Dictionary<string,int> tbl = new();
                int nextPeriod = 1;
                foreach (string id in sultanDeaths.Values)
                {
                    tbl.Add(id, nextPeriod);
                    nextPeriod += 1;
                }
                GameLog.LogDebug(
                    $"Computed a sultan history {nextPeriod-1} long"
                );
                _sultanPeriodTable = tbl;
            }
            return _sultanPeriodTable;
        }
    }

    private static int? _secretLocationIndex(int secretsFound, int secretsPerLoc, int maxSecrets)
    {
        if (secretsFound == maxSecrets)
        {
            return maxSecrets / secretsPerLoc + (maxSecrets % secretsPerLoc == 0 ? 0 : 1);
        }
        if (secretsFound < maxSecrets && secretsFound % secretsPerLoc == 0)
        {
            return secretsFound / secretsPerLoc;
        }
        return null;
    }

    public override bool HandleEvent(SecretVisibilityChangedEvent E)
    {
        if (E.Entry is JournalSultanNote evtJsn)
        {
            string evtSultanId = evtJsn.SultanID;
            if (sultanPeriodTable[evtSultanId] is int sultanPeriod)
            {
                int seenAllSultans = 0;
                int seenSameSultan = 0;
                foreach (JournalSultanNote jsn in JournalAPI.SultanNotes)
                {
                    if (jsn.Revealed && !jsn.Attributes.Contains("sultanTombPropaganda"))
                    {
                        seenAllSultans += 1;
                        if (jsn.SultanID == evtSultanId)
                        {
                            seenSameSultan += 1;
                        }
                    }

                }

                if (
                    _secretLocationIndex(
                        seenAllSultans,
                        APGame.Instance.Data.SultanSecretsPerLocation,
                        APGame.Instance.Data.SultanSecretsRequired
                    ) is int n
                )
                {
                    APGame.Instance.CheckLocation($"{n} Sultan Lore");
                }

                if (sultanPeriod == 6)
                {
                    var lookup = APGame.Instance.Data.ReshephSecretsRequired > 0 ?
                        _secretLocationIndex(
                            seenSameSultan,
                            APGame.Instance.Data.ReshephSecretsPerLocation,
                            APGame.Instance.Data.ReshephSecretsRequired
                        ) :
                        _secretLocationIndex(
                            seenSameSultan,
                            APGame.Instance.Data.PeriodSultanSecretsPerLocation,
                            APGame.Instance.Data.PeriodSultanSecretsRequired
                        );
                    if (lookup is int m)
                    {
                        APGame.Instance.CheckLocation($"{m} Resheph Lore");
                    }
                }
                else
                {
                    if (
                        _secretLocationIndex(
                            seenSameSultan,
                            APGame.Instance.Data.PeriodSultanSecretsPerLocation,
                            APGame.Instance.Data.PeriodSultanSecretsRequired
                        ) is int m
                    )
                    {
                        string count = m == 1 ? "" : $"{m} ";
                        string periodName = sultanPeriod switch
                        {
                            1 => "First",
                            2 => "Second",
                            3 => "Third",
                            4 => "Fourth",
                            _ => "Fifth"
                        };
                        APGame.Instance.CheckLocation($"{count} Lore from the {periodName} Sultanate");
                    }
                }
            }
        }

        return true;
    }
}
