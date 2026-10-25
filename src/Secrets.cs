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

                if (APGame.Instance.Data.AggregateSecretLocations.IndexOf(seenAllSultans) is int n)
                {
                    APGame.Instance.CheckLocation($"{n} Sultan Lore");
                }

                if (sultanPeriod == 6 && APGame.Instance.Data.ReshephSecretLocations != null)
                    {
                    if (APGame.Instance.Data.ReshephSecretLocations.IndexOf(seenSameSultan) is int m)
                    {
                        APGame.Instance.CheckLocation($"{m} Resheph Lore");
                    }
                }
                else
                {
                    if (APGame.Instance.Data.PerPeriodSecretLocations.IndexOf(seenSameSultan) is int m)
                    {
                        string count = m == 1 ? "Lore from the " : $"{m} Lore from the ";
                        string periodName = sultanPeriod switch
                        {
                            1 => "First",
                            2 => "Second",
                            3 => "Third",
                            4 => "Fourth",
                            _ => "Fifth"
                        };
                        APGame.Instance.CheckLocation($"{count}{periodName} Sultanate");
                    }
                }
            }
        }

        return true;
    }
}
