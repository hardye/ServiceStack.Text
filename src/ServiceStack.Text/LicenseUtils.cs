﻿// Copyright (c) Service Stack LLC. All Rights Reserved.
// License: https://raw.github.com/ServiceStack/ServiceStack/master/license.txt

using System;
using System.IO;
using System.Text.RegularExpressions;
using ServiceStack.Text;

namespace ServiceStack
{
    public class LicenseException : Exception
    {
        public LicenseException(string message) : base(message) { }
    }

    public enum LicenseType
    {
        Free,
        Indie,
        Business,
        Enterprise,
        Text,
        OrmLite,
        Redis
    }

    [Flags]
    public enum LicenseFeature : long
    {
        None = 0,
        All = Premium | Text | Client | Common | Redis | OrmLite | ServiceStack | Server | Razor | Admin,
        RedisSku = Redis | Text,
        OrmLiteSku = OrmLite | Text,
        Free = None,
        Indie = All,
        Business = All,
        Enterprise = All,
        Premium = 1 << 0,
        Text = 1 << 1,
        Client = 1 << 2,
        Common = 1 << 3,
        Redis = 1 << 4,
        OrmLite = 1 << 5,
        ServiceStack = 1 << 6,
        Server = 1 << 7,
        Razor = 1 << 8,
        Admin = 1 << 9,
    }

    public enum QuotaType
    {
        Operations,      //ServiceStack
        Types,           //Text, Redis
        Fields,          //ServiceStack, Text, Redis, OrmLite
        RequestsPerHour, //Redis
        Tables,          //OrmLite
        PremiumFeature,  //AdminUI, Advanced Redis APIs, etc
    }

    /// <summary>
    /// Public Code API to register commercial license for ServiceStack.
    /// </summary>
    public static class Licensing
    {
        public static void RegisterLicense(string licenseKeyText)
        {
            LicenseUtils.RegisterLicense(licenseKeyText);
        }

        public static void RegisterLicenseFromFile(string filePath)
        {
            if (!filePath.FileExists())
                throw new LicenseException("License file does not exist: " + filePath);

            var licenseKeyText = filePath.ReadAllText();
            LicenseUtils.RegisterLicense(licenseKeyText);
        }
    }

    public class LicenseKey
    {
        public string Ref { get; set; }
        public string Name { get; set; }
        public LicenseType Type { get; set; }
        public string Hash { get; set; }
        public DateTime Expiry { get; set; }
    }

    /// <summary>
    /// Internal Utilities to verify licensing
    /// </summary>
    public static class LicenseUtils
    {
        public const string RuntimePublicKey = "<RSAKeyValue><Modulus>nkqwkUAcuIlVzzOPENcQ+g5ALCe4LyzzWv59E4a7LuOM1Nb+hlNlnx2oBinIkvh09EyaxIX2PmaY0KtyDRIh+PoItkKeJe/TydIbK/bLa0+0Axuwa0MFShE6HdJo/dynpODm64+Sg1XfhICyfsBBSxuJMiVKjlMDIxu9kDg7vEs=</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>";
        public const string LicensePublicKey = "<RSAKeyValue><Modulus>w2fTTfr2SrGCclwLUkrbH0XsIUpZDJ1Kei2YUwYGmIn5AUyCPLTUv3obDBUBFJKLQ61Khs7dDkXlzuJr5tkGQ0zS0PYsmBPAtszuTum+FAYRH4Wdhmlfqu1Z03gkCIo1i11TmamN5432uswwFCVH60JU3CpaN97Ehru39LA1X9E=</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>";

        private const string ContactDetails = " Please see servicestack.net or contact team@servicestack.net for more details.";

        static LicenseUtils()
        {
            PclExport.Instance.RegisterLicenseFromConfig();
        }

        public static class ErrorMessages
        {
            private const string UpgradeInstructions = " Please see https://servicestack.net to upgrade to a commercial license or visit https://github.com/ServiceStackV3/ServiceStackV3 to revert back to the free ServiceStack v3.";
            internal const string ExceededTextTypes = "The free-quota limit on '{0} ServiceStack.Text Types' has been reached." + UpgradeInstructions;
            internal const string ExceededRedisTypes = "The free-quota limit on '{0} Redis Types' has been reached." + UpgradeInstructions;
            internal const string ExceededRedisRequests = "The free-quota limit on '{0} Redis requests per hour' has been reached." + UpgradeInstructions;
            internal const string ExceededOrmLiteTables = "The free-quota limit on '{0} OrmLite Tables' has been reached." + UpgradeInstructions;
            internal const string ExceededServiceStackOperations = "The free-quota limit on '{0} ServiceStack Operations' has been reached." + UpgradeInstructions;
            internal const string ExceededAdminUi = "The Admin UI is a commerical-only premium feature." + UpgradeInstructions;
            internal const string ExceededPremiumFeature = "Unauthorized use of a commerical-only premium feature." + UpgradeInstructions;
            public const string UnauthorizedAccessRequest = "Unauthorized access request of a licensed feature." + UpgradeInstructions;
        }

        public static class FreeQuotas
        {
            public const int ServiceStackOperations = 10;
            public const int TypeFields = 20;
            public const int TextTypes = 20;
            public const int RedisTypes = 20;
            public const int RedisRequestPerHour = 6000;
            public const int OrmLiteTables = 10;
            public const int PremiumFeature = 0;
        }

        public static void AssertEvaluationLicense()
        {
            if (DateTime.UtcNow > new DateTime(2013, 12, 31))
                throw new LicenseException("The evaluation license for this software has expired. " +
                    "See https://servicestack.net to upgrade to a valid license.");
        }

        private static LicenseKey __activatedLicense;
        public static void RegisterLicense(string licenseKeyText)
        {
            string cutomerId = null;
            try
            {
                var parts = licenseKeyText.SplitOnFirst('-');
                cutomerId = parts[0];

                LicenseKey key;
                using (new AccessToken(LicenseFeature.Text))
                {
                    key = PclExport.Instance.VerifyLicenseKeyText(licenseKeyText);
                }

                var releaseDate = Env.GetReleaseDate();
                if (releaseDate > key.Expiry)
                    throw new LicenseException("This license has expired on {0} and is not valid for use with this release."
                        .Fmt(key.Expiry.ToString("d")) + ContactDetails);

                __activatedLicense = key;
            }
            catch (Exception ex)
            {
                if (ex is LicenseException)
                    throw;

                var msg = "This license is invalid." + ContactDetails;
                if (!string.IsNullOrEmpty(cutomerId))
                    msg += " The id for this license is '{0}'".Fmt(cutomerId);

                throw new LicenseException(msg);
            }
        }

        public static void RemoveLicense()
        {
            __activatedLicense = null;
        }

        public static LicenseFeature ActivatedLicenseFeatures()
        {
            return __activatedLicense != null ? __activatedLicense.GetLicensedFeatures() : LicenseFeature.None;
        }

        public static void ApprovedUsage(LicenseFeature licenseFeature, LicenseFeature requestedFeature,
            int allowedUsage, int actualUsage, string message)
        {
            var hasFeature = (requestedFeature & licenseFeature) == requestedFeature;
            if (hasFeature)
                return;

            if (actualUsage > allowedUsage)
                throw new LicenseException(message.Fmt(allowedUsage));
        }

        public static bool HasLicensedFeature(LicenseFeature feature)
        {
            var licensedFeatures = ActivatedLicenseFeatures();
            return (feature & licensedFeatures) == feature;
        }

        public static void AssertValidUsage(LicenseFeature feature, QuotaType quotaType, int count)
        {
            var licensedFeatures = ActivatedLicenseFeatures();
            if ((LicenseFeature.All & licensedFeatures) == LicenseFeature.All) //Standard Usage
                return;

            if (AccessTokenScope != null)
            {
                if ((feature & AccessTokenScope.tempFeatures) == feature)
                    return;
            }

            //Free Quotas
            switch (feature)
            {
                case LicenseFeature.Text:
                    switch (quotaType)
                    {
                        case QuotaType.Types:
                            ApprovedUsage(licensedFeatures, feature, FreeQuotas.TextTypes, count, ErrorMessages.ExceededTextTypes);
                            return;
                    }
                    break;

                case LicenseFeature.Redis:
                    switch (quotaType)
                    {
                        case QuotaType.Types:
                            ApprovedUsage(licensedFeatures, feature, FreeQuotas.RedisTypes, count, ErrorMessages.ExceededRedisTypes);
                            return;
                        case QuotaType.RequestsPerHour:
                            ApprovedUsage(licensedFeatures, feature, FreeQuotas.RedisRequestPerHour, count, ErrorMessages.ExceededRedisRequests);
                            return;
                    }
                    break;

                case LicenseFeature.OrmLite:
                    switch (quotaType)
                    {
                        case QuotaType.Tables:
                            ApprovedUsage(licensedFeatures, feature, FreeQuotas.OrmLiteTables, count, ErrorMessages.ExceededOrmLiteTables);
                            return;
                    }
                    break;

                case LicenseFeature.ServiceStack:
                    switch (quotaType)
                    {
                        case QuotaType.Operations:
                            ApprovedUsage(licensedFeatures, feature, FreeQuotas.ServiceStackOperations, count, ErrorMessages.ExceededServiceStackOperations);
                            return;
                    }
                    break;

                case LicenseFeature.Admin:
                    switch (quotaType)
                    {
                        case QuotaType.PremiumFeature:
                            ApprovedUsage(licensedFeatures, feature, FreeQuotas.PremiumFeature, count, ErrorMessages.ExceededAdminUi);
                            return;
                    }
                    break;

                case LicenseFeature.Premium:
                    switch (quotaType)
                    {
                        case QuotaType.PremiumFeature:
                            ApprovedUsage(licensedFeatures, feature, FreeQuotas.PremiumFeature, count, ErrorMessages.ExceededPremiumFeature);
                            return;
                    }
                    break;
            }

            throw new LicenseException("Unknown Quota Usage: {0}, {1}".Fmt(feature, quotaType));
        }

        public static LicenseFeature GetLicensedFeatures(this LicenseKey key)
        {
            switch (key.Type)
            {
                case LicenseType.Free:
                    return LicenseFeature.Free;
                case LicenseType.Indie:
                    return LicenseFeature.Indie;
                case LicenseType.Business:
                    return LicenseFeature.Business;
                case LicenseType.Enterprise:
                    return LicenseFeature.Enterprise;
                case LicenseType.Text:
                    return LicenseFeature.Text;
                case LicenseType.OrmLite:
                    return LicenseFeature.OrmLite;
                case LicenseType.Redis:
                    return LicenseFeature.Redis;
            }
            throw new ArgumentException("Unknown License Type: " + key.Type);
        }

        public static LicenseKey ToLicenseKey(this string licenseKeyText)
        {
            licenseKeyText = Regex.Replace(licenseKeyText, @"\s+", "");
            var parts = licenseKeyText.SplitOnFirst('-');
            var refId = parts[0];
            var base64 = parts[1];
            var jsv = Convert.FromBase64String(base64).FromUtf8Bytes();
            var key = jsv.FromJsv<LicenseKey>();

            if (key.Ref != refId)
                throw new LicenseException("The license '{0}' is not assigned to CustomerId '{1}'.".Fmt(base64));

            return key;
        }

        public static string GetHashKeyToSign(this LicenseKey key)
        {
            return "{0}:{1}:{2}:{3}".Fmt(key.Ref, key.Name, key.Expiry.ToString("yyyy-MM-dd"), key.Type);
        }

        public static Exception GetInnerMostException(this Exception ex)
        {
            //Extract true exception from static intializers (e.g. LicenseException)
            while (ex.InnerException != null)
            {
                ex = ex.InnerException;
            }
            return ex;
        }

        [ThreadStatic]
        private static AccessToken AccessTokenScope;
        private class AccessToken : IDisposable
        {
            private readonly AccessToken prevToken;
            internal readonly LicenseFeature tempFeatures;
            internal AccessToken(LicenseFeature requested)
            {
                prevToken = AccessTokenScope;
                AccessTokenScope = this;
                tempFeatures = requested;
            }

            public void Dispose()
            {
                AccessTokenScope = prevToken;
            }
        }

        public static IDisposable RequestAccess(object accessToken, LicenseFeature srcFeature, LicenseFeature requestedAccess)
        {
            var accessType = accessToken.GetType();
            if (srcFeature != LicenseFeature.Client || requestedAccess != LicenseFeature.Text
                || accessToken == null || accessType.FullName != "ServiceStack.ServiceClientBase+AccessToken")
                throw new LicenseException(ErrorMessages.UnauthorizedAccessRequest);

            PclExport.Instance.VerifyInAssembly(accessType, "ServiceStack.Client.dll");

            return new AccessToken(requestedAccess);
        }
    }
}