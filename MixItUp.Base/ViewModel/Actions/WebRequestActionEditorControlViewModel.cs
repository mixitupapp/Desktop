using MixItUp.Base.Model.Actions;
using MixItUp.Base.Util;
using MixItUp.Base.ViewModels;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace MixItUp.Base.ViewModel.Actions
{
    public class WebRequestActionJSONToSpecialIdentifierViewModel : UIViewModelBase
    {
        public string JSONParameterName { get; set; }
        public string SpecialIdentifierName { get; set; }

        public ICommand DeleteJSONParameterCommand { get; private set; }

        private WebRequestActionEditorControlViewModel viewModel;

        public WebRequestActionJSONToSpecialIdentifierViewModel(string jsonParameterName, string specialIdentifierName, WebRequestActionEditorControlViewModel viewModel)
            : this(viewModel)
        {
            this.JSONParameterName = jsonParameterName;
            this.SpecialIdentifierName = specialIdentifierName;
        }

        public WebRequestActionJSONToSpecialIdentifierViewModel(WebRequestActionEditorControlViewModel viewModel)
        {
            this.viewModel = viewModel;
            this.DeleteJSONParameterCommand = this.CreateCommand(() =>
            {
                this.viewModel.JSONParameters.Remove(this);
            });
        }
    }

    public class WebRequestActionCustomHeaderViewModel : UIViewModelBase
    {
        public string HeaderName { get; set; }
        public string HeaderValue { get; set; }

        public ICommand DeleteHeaderCommand { get; private set; }

        private WebRequestActionEditorControlViewModel viewModel;

        public WebRequestActionCustomHeaderViewModel(string headerName, string headerValue, WebRequestActionEditorControlViewModel viewModel)
            : this(viewModel)
        {
            this.HeaderName = headerName;
            this.HeaderValue = headerValue;
        }

        public WebRequestActionCustomHeaderViewModel(WebRequestActionEditorControlViewModel viewModel)
        {
            this.viewModel = viewModel;
            this.DeleteHeaderCommand = this.CreateCommand(() =>
            {
                this.viewModel.CustomHeaders.Remove(this);
            });
        }
    }

    public class WebRequestActionEditorControlViewModel : ActionEditorControlViewModelBase
    {
        public override ActionTypeEnum Type { get { return ActionTypeEnum.WebRequest; } }

        public string RequestURL
        {
            get { return this.requestURL; }
            set
            {
                this.requestURL = value;
                this.NotifyPropertyChanged();
            }
        }
        private string requestURL;

        public IEnumerable<HttpMethodEnum> HttpMethods { get { return EnumHelper.GetEnumList<HttpMethodEnum>(); } }

        public HttpMethodEnum SelectedHttpMethod
        {
            get { return this.selectedHttpMethod; }
            set
            {
                this.selectedHttpMethod = value;
                this.NotifyPropertyChanged();
                this.NotifyPropertyChanged("ShowRequestBody");
            }
        }
        private HttpMethodEnum selectedHttpMethod = HttpMethodEnum.GET;

        public bool ShowRequestBody
        {
            get
            {
                return this.SelectedHttpMethod == HttpMethodEnum.POST || this.SelectedHttpMethod == HttpMethodEnum.PUT;
            }
        }

        public string RequestBody
        {
            get { return this.requestBody; }
            set
            {
                this.requestBody = value;
                this.NotifyPropertyChanged();
            }
        }
        private string requestBody;

        public IEnumerable<WebRequestResponseParseTypeEnum> ResponseParseTypes { get { return EnumHelper.GetEnumList<WebRequestResponseParseTypeEnum>(); } }

        public WebRequestResponseParseTypeEnum SelectedResponseParseType
        {
            get { return this.selectedResponseParseType; }
            set
            {
                this.selectedResponseParseType = value;
                this.NotifyPropertyChanged();
                this.NotifyPropertyChanged("ShowPlainTextGrid");
                this.NotifyPropertyChanged("ShowJSONGrid");
            }
        }
        public WebRequestResponseParseTypeEnum selectedResponseParseType;

        public bool ShowPlainTextGrid { get { return this.SelectedResponseParseType == WebRequestResponseParseTypeEnum.PlainText; } }

        public bool ShowJSONGrid { get { return this.SelectedResponseParseType == WebRequestResponseParseTypeEnum.JSONToSpecialIdentifiers; } }

        public ICommand AddJSONParameterCommand { get; private set; }
        public ICommand AddCustomHeaderCommand { get; private set; }

        public ObservableCollection<WebRequestActionJSONToSpecialIdentifierViewModel> JSONParameters { get; set; } = new ObservableCollection<WebRequestActionJSONToSpecialIdentifierViewModel>();
        public ObservableCollection<WebRequestActionCustomHeaderViewModel> CustomHeaders { get; set; } = new ObservableCollection<WebRequestActionCustomHeaderViewModel>();

        public WebRequestActionEditorControlViewModel(WebRequestActionModel action)
            : base(action)
        {
            this.RequestURL = action.Url;
            this.SelectedHttpMethod = action.HttpMethod;
            this.RequestBody = action.RequestBody;
            this.SelectedResponseParseType = action.ResponseType;
            if (this.ShowJSONGrid)
            {
                this.JSONParameters.AddRange(action.JSONToSpecialIdentifiers.Select(kvp => new WebRequestActionJSONToSpecialIdentifierViewModel(kvp.Key, kvp.Value, this)));
            }
            if (action.CustomHeaders != null)
            {
                this.CustomHeaders.AddRange(action.CustomHeaders.Select(kvp => new WebRequestActionCustomHeaderViewModel(kvp.Key, kvp.Value, this)));
            }
        }

        public WebRequestActionEditorControlViewModel() : base() { }

        protected override async Task OnOpenInternal()
        {
            this.AddJSONParameterCommand = this.CreateCommand(() =>
            {
                this.JSONParameters.Add(new WebRequestActionJSONToSpecialIdentifierViewModel(this));
            });
            this.AddCustomHeaderCommand = this.CreateCommand(() =>
            {
                this.CustomHeaders.Add(new WebRequestActionCustomHeaderViewModel(this));
            });
            await base.OnOpenInternal();
        }

        public override Task<Result> Validate()
        {
            if (string.IsNullOrEmpty(this.RequestURL))
            {
                return Task.FromResult(new Result(MixItUp.Base.Resources.WebRequestActionMissingURL));
            }

            if (this.ShowJSONGrid)
            {
                HashSet<string> parameters = new HashSet<string>();
                foreach (WebRequestActionJSONToSpecialIdentifierViewModel jsonParameter in this.JSONParameters)
                {
                    if (string.IsNullOrEmpty(jsonParameter.JSONParameterName) || string.IsNullOrEmpty(jsonParameter.SpecialIdentifierName))
                    {
                        return Task.FromResult(new Result(MixItUp.Base.Resources.WebRequestActionMissingJSONParameter));
                    }

                    jsonParameter.SpecialIdentifierName = jsonParameter.SpecialIdentifierName.Replace("$", "");
                    if (!SpecialIdentifierStringBuilder.IsValidSpecialIdentifier(jsonParameter.SpecialIdentifierName))
                    {
                        return Task.FromResult(new Result(MixItUp.Base.Resources.WebRequestActionInvalidJSONSpecialIdentifier));
                    }

                    if (parameters.Contains(jsonParameter.JSONParameterName))
                    {
                        return Task.FromResult(new Result(MixItUp.Base.Resources.WebRequestActionDuplicateJSONParameter));
                    }
                    parameters.Add(jsonParameter.JSONParameterName);
                }
            }

            HashSet<string> headerNames = new HashSet<string>();
            foreach (WebRequestActionCustomHeaderViewModel header in this.CustomHeaders)
            {
                if (string.IsNullOrEmpty(header.HeaderName))
                {
                    return Task.FromResult(new Result("Custom header name cannot be empty"));
                }

                if (headerNames.Contains(header.HeaderName))
                {
                    return Task.FromResult(new Result($"Duplicate custom header name: {header.HeaderName}"));
                }
                headerNames.Add(header.HeaderName);
            }

            return Task.FromResult(new Result());
        }

        protected override Task<ActionModelBase> GetActionInternal()
        {
            Dictionary<string, string> customHeaders = new Dictionary<string, string>();
            foreach (WebRequestActionCustomHeaderViewModel header in this.CustomHeaders)
            {
                if (!string.IsNullOrEmpty(header.HeaderName))
                {
                    customHeaders[header.HeaderName] = header.HeaderValue ?? string.Empty;
                }
            }

            if (this.ShowJSONGrid)
            {
                Dictionary<string, string> jsonParameters = new Dictionary<string, string>();
                foreach (WebRequestActionJSONToSpecialIdentifierViewModel jsonParameter in this.JSONParameters)
                {
                    jsonParameters[jsonParameter.JSONParameterName] = jsonParameter.SpecialIdentifierName;
                }
                return Task.FromResult<ActionModelBase>(new WebRequestActionModel(this.RequestURL, jsonParameters, this.SelectedHttpMethod, customHeaders, this.RequestBody));
            }
            else
            {
                return Task.FromResult<ActionModelBase>(new WebRequestActionModel(this.RequestURL, this.SelectedResponseParseType, this.SelectedHttpMethod, customHeaders, this.RequestBody));
            }
        }
    }
}