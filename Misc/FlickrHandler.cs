using FlickrNet;
using GeistDesWaldes.Attributes;
using GeistDesWaldes.Configuration;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GeistDesWaldes.Misc
{
    public class FlickrHandler : BaseHandler
    {
        public const string PIC_SOURCE_MAIN = @"https://ww.flickr.com";
        public const string PIC_SOURCE_ICON = @"https://combo.staticflickr.com/pw/images/favicons/favicon-32.png";

        private static readonly SafetyLevel _searchSafetyLevel = SafetyLevel.Safe | SafetyLevel.Moderate;
        private static readonly LicenseType[] _photoLicenses =
        [
            LicenseType.NoKnownCopyrightRestrictions,
            LicenseType.PublicDomainMark,
            LicenseType.PublicDomainDedicationCC0,
            LicenseType.AttributionCC, 
            LicenseType.AttributionNoDerivativesCC, 
            LicenseType.AttributionNoncommercialCC, 
            LicenseType.AttributionNoncommercialNoDerivativesCC
        ];
        
        private readonly ConcurrentDictionary<string, List<Photo>> _imageCache = new ();
        private Flickr _flickr;


        public FlickrHandler(Server server) : base(server)
        {
        }

        
        public override async Task OnServerStartUp()
        {
            await base.OnServerStartUp();
            _flickr = new Flickr(ConfigurationHandler.Shared.Secrets.FlickrApiKey);
        }

        public override async Task OnServerShutdown()
        {
            await base.OnServerShutdown();
            _flickr = null;
        }


        public async Task<CustomRuntimeResult<Photo>> GetRandomImage(string keyword)
        {
            keyword = keyword.ToLower();

            if (!_imageCache.ContainsKey(keyword))
                _imageCache.TryAdd(keyword, []);

            _imageCache.TryGetValue(keyword, out List<Photo> images);

            if (images == null || images.Count == 0)
            {
                CustomRuntimeResult<PhotoCollection> updateResult = await UpdateCacheEntry(keyword);

                if (!updateResult.IsSuccess)
                    return CustomRuntimeResult<Photo>.FromError(updateResult.Reason);

                images ??= [];
                images.AddRange(updateResult.ResultValue);
            }

            int idx = Launcher.Random.Next(0, images.Count);
            Photo entry = images[idx];
            images.RemoveAt(idx);
            
            return CustomRuntimeResult<Photo>.FromSuccess(value: entry);
        }

        private async Task<CustomRuntimeResult<PhotoCollection>> UpdateCacheEntry(string keyword)
        {
            PhotoSearchOptions searchOptions = new()
            {
                Tags = keyword,
                Page = Launcher.Random.Next(0, 10),
                PerPage = 10,
                SafeSearch = _searchSafetyLevel,
                MediaType = MediaType.Photos,
                Extras = PhotoSearchExtras.SmallUrl | PhotoSearchExtras.OwnerName,
                PrivacyFilter = PrivacyFilter.PublicPhotos,
                ContentType = ContentTypeSearch.PhotosAndOthers
            };

            foreach (LicenseType lType in _photoLicenses)
            {
                searchOptions.Licenses.Add(lType);
            }

            int timeoutInSeconds = ConfigurationHandler.Shared.WebClientTimeoutInSeconds;

            Task<PhotoCollection> searchResult = _flickr.PhotosSearchAsync(searchOptions);

            do
            {
                await Task.Delay(1000);
                timeoutInSeconds -= 1;
            }
            while (!searchResult.IsCompleted && timeoutInSeconds > 0);

            if (timeoutInSeconds < 1)
                return CustomRuntimeResult<PhotoCollection>.FromError($"Timed out waiting for Flickr search!");

            if (!searchResult.IsCompletedSuccessfully || searchResult.Result == null || searchResult.Result.Count == 0)
                return CustomRuntimeResult<PhotoCollection>.FromError($"Could not find images tagged '{keyword}'!{(searchResult.IsFaulted ? $"\n{searchResult.Exception}" : "")}");

            return CustomRuntimeResult<PhotoCollection>.FromSuccess(value: searchResult.Result);
        }
    }
}
