using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace MixItUp.Distribution.Core
{
    public sealed class DistributionClient
    {
        private readonly string baseUrl;

        public DistributionClient(string baseUrl)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                throw new ArgumentException("Base URL is required.", nameof(baseUrl));
            }

            this.baseUrl = baseUrl.TrimEnd('/');
        }

        public async Task<UpdatePackageInfo> GetLatestPackageAsync(
            string productSlug,
            string platform,
            string channel,
            CancellationToken cancellationToken = default
        )
        {
            if (string.IsNullOrWhiteSpace(productSlug))
            {
                throw new ArgumentException("Product slug is required.", nameof(productSlug));
            }

            if (string.IsNullOrWhiteSpace(platform))
            {
                throw new ArgumentException("Platform is required.", nameof(platform));
            }

            if (string.IsNullOrWhiteSpace(channel))
            {
                throw new ArgumentException("Channel is required.", nameof(channel));
            }

            string manifestUrl = BuildManifestUrl(productSlug, platform, channel);

            try
            {
                using (HttpClient client = CreateHttpClient(TimeSpan.FromSeconds(15)))
                using (
                    HttpResponseMessage response = await client
                        .GetAsync(manifestUrl, cancellationToken)
                        .ConfigureAwait(false)
                )
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        string errorBody = await response.Content
                            .ReadAsStringAsync()
                            .ConfigureAwait(false);

                        throw new DistributionException(
                            $"Manifest request to '{manifestUrl}' failed with {(int)response.StatusCode} {response.StatusCode}: {errorBody}"
                        )
                        {
                            StatusCode = response.StatusCode,
                            Endpoint = manifestUrl,
                        };
                    }

                    string responseBody = await response.Content
                        .ReadAsStringAsync()
                        .ConfigureAwait(false);

                    UpdateManifestModel manifest =
                        Newtonsoft.Json.JsonConvert.DeserializeObject<UpdateManifestModel>(
                            responseBody
                        );
                    if (manifest == null)
                    {
                        throw new DistributionException(
                            $"Manifest response from '{manifestUrl}' was empty or invalid."
                        )
                        {
                            Endpoint = manifestUrl,
                        };
                    }

                    UpdatePlatformModel platformEntry = manifest.Platforms?.FirstOrDefault(p =>
                        string.Equals(
                            p.Platform,
                            platform,
                            StringComparison.OrdinalIgnoreCase
                        )
                    );

                    if (platformEntry == null)
                    {
                        throw new DistributionException(
                            $"Manifest for '{productSlug}' did not include platform '{platform}'."
                        )
                        {
                            Endpoint = manifestUrl,
                        };
                    }

                    UpdateFileModel fileEntry =
                        platformEntry.Files?.FirstOrDefault(f =>
                            f != null
                            && !string.IsNullOrWhiteSpace(f.Url)
                            && string.Equals(
                                f.ContentType,
                                "application/zip",
                                StringComparison.OrdinalIgnoreCase
                            )
                        )
                        ?? platformEntry.Files?.FirstOrDefault(f =>
                            !string.IsNullOrWhiteSpace(f?.Url)
                        );
                    if (fileEntry == null)
                    {
                        throw new DistributionException(
                            $"Manifest for '{productSlug}' platform '{platform}' did not include a downloadable file."
                        )
                        {
                            Endpoint = manifestUrl,
                        };
                    }

                    Uri downloadUri = BuildDownloadUri(fileEntry.Url);
                    if (downloadUri == null)
                    {
                        throw new DistributionException(
                            $"Unable to construct download URI from '{fileEntry.Url}'."
                        )
                        {
                            Endpoint = manifestUrl,
                        };
                    }

                    return new UpdatePackageInfo(
                        manifest.Version ?? string.Empty,
                        manifest.Channel ?? channel,
                        platformEntry.Platform,
                        fileEntry,
                        downloadUri,
                        manifest.SchemaVersion,
                        manifest.ReleaseType,
                        manifest.ReleaseNotes
                    );
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (DistributionException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new DistributionException(
                    $"Manifest request to '{manifestUrl}' failed: {ex.Message}",
                    ex
                )
                {
                    Endpoint = manifestUrl,
                };
            }
        }

        public async Task<byte[]> DownloadPackageAsync(
            Uri downloadUri,
            TimeSpan timeout,
            IProgress<int> progress = null,
            CancellationToken cancellationToken = default
        )
        {
            if (downloadUri == null)
            {
                throw new ArgumentNullException(nameof(downloadUri));
            }

            try
            {
                using (HttpClient client = CreateHttpClient(timeout))
                using (
                    HttpResponseMessage response = await client
                        .GetAsync(downloadUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                        .ConfigureAwait(false)
                )
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        string errorBody = await response.Content
                            .ReadAsStringAsync()
                            .ConfigureAwait(false);

                        throw new DistributionException(
                            $"Download request to '{downloadUri}' failed with {(int)response.StatusCode} {response.StatusCode}: {errorBody}"
                        )
                        {
                            StatusCode = response.StatusCode,
                            Endpoint = downloadUri.ToString(),
                        };
                    }

                    long? contentLength = response.Content.Headers.ContentLength;
                    progress?.Report(0);

                    using (
                        Stream responseStream = await response.Content
                            .ReadAsStreamAsync()
                            .ConfigureAwait(false)
                    )
                    using (MemoryStream memoryStream = new MemoryStream())
                    {
                        byte[] buffer = new byte[81920];
                        long totalRead = 0;
                        int bytesRead;
                        while (
                            (bytesRead = await responseStream
                                .ReadAsync(buffer, 0, buffer.Length, cancellationToken)
                                .ConfigureAwait(false)) > 0
                        )
                        {
                            await memoryStream
                                .WriteAsync(buffer, 0, bytesRead)
                                .ConfigureAwait(false);
                            totalRead += bytesRead;

                            if (contentLength.HasValue && contentLength.Value > 0)
                            {
                                int percent = (int)
                                    Math.Min(100, (totalRead * 100) / contentLength.Value);
                                progress?.Report(percent);
                            }
                        }

                        progress?.Report(100);

                        return memoryStream.ToArray();
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (DistributionException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new DistributionException(
                    $"Download request to '{downloadUri}' failed: {ex.Message}",
                    ex
                )
                {
                    Endpoint = downloadUri.ToString(),
                };
            }
        }

        public string BuildManifestUrl(string productSlug, string platform, string channel)
        {
            if (string.IsNullOrWhiteSpace(productSlug))
            {
                throw new ArgumentException("Product slug is required.", nameof(productSlug));
            }

            if (string.IsNullOrWhiteSpace(platform))
            {
                throw new ArgumentException("Platform is required.", nameof(platform));
            }

            if (string.IsNullOrWhiteSpace(channel))
            {
                throw new ArgumentException("Channel is required.", nameof(channel));
            }

            Uri manifestBaseUri = new Uri("https://files.mixitupapp.com", UriKind.Absolute);

            string relativePath = string.Join(
                "/",
                "apps",
                Uri.EscapeDataString(productSlug),
                Uri.EscapeDataString(platform),
                Uri.EscapeDataString(channel),
                "latest"
            );

            Uri manifestUri = new Uri(manifestBaseUri, relativePath);
            return manifestUri.ToString();
        }

        public Uri BuildDownloadUri(string fileUrl)
        {
            if (string.IsNullOrWhiteSpace(fileUrl))
            {
                return null;
            }

            if (Uri.TryCreate(fileUrl, UriKind.Absolute, out Uri absoluteUri))
            {
                return absoluteUri;
            }

            if (!Uri.TryCreate(this.baseUrl, UriKind.Absolute, out Uri baseUri))
            {
                return null;
            }

            string relativePath = fileUrl.StartsWith("/", StringComparison.Ordinal)
                ? fileUrl
                : "/" + fileUrl;

            if (Uri.TryCreate(baseUri, relativePath, out Uri combinedUri))
            {
                return combinedUri;
            }

            return null;
        }

        private static HttpClient CreateHttpClient(TimeSpan timeout)
        {
            HttpClient client = new HttpClient();
            client.Timeout = timeout;
            return client;
        }
    }
}
