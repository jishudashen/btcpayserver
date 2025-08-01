using System.Collections.Generic;
using NBitcoin;
using NBXplorer.DerivationStrategy;
using Newtonsoft.Json.Linq;

namespace BTCPayServer
{
    public static class UtilitiesExtensions
    {
        public static bool TryAdd(this JObject obj, string key, JToken value)
        {
            if (obj.TryGetValue(key, out var existing))
            {
                return false;
            }
            obj.Add(key, value);
            return true;
        }

        public static void AddRange<T>(this HashSet<T> hashSet, IEnumerable<T> items)
        {
            foreach (var item in items)
            {
                hashSet.Add(item);
            }
        }

        public static ScriptPubKeyType ScriptPubKeyType(this DerivationStrategyBase derivationStrategyBase)
        {
            if (derivationStrategyBase is TaprootDerivationStrategy)
                return NBitcoin.ScriptPubKeyType.TaprootBIP86;
            if (IsSegwitCore(derivationStrategyBase))
            {
                return NBitcoin.ScriptPubKeyType.Segwit;
            }

            return (derivationStrategyBase is P2SHDerivationStrategy p2shStrat && IsSegwitCore(p2shStrat.Inner))
                ? NBitcoin.ScriptPubKeyType.SegwitP2SH
                : NBitcoin.ScriptPubKeyType.Legacy;
        }

        private static bool IsSegwitCore(DerivationStrategyBase derivationStrategyBase)
        {
            return (derivationStrategyBase is P2WSHDerivationStrategy) ||
                   (derivationStrategyBase is DirectDerivationStrategy direct) && direct.Segwit;
        }
    }
}
