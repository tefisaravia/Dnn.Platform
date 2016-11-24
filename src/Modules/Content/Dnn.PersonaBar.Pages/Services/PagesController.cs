﻿#region Copyright

// DotNetNuke® - http://www.dotnetnuke.com
// Copyright (c) 2002-2016
// by DotNetNuke Corporation
// All Rights Reserved

#endregion

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Web.UI.WebControls;
using Dnn.PersonaBar.Library;
using Dnn.PersonaBar.Pages.Components;
using Dnn.PersonaBar.Pages.Components.Dto;
using Dnn.PersonaBar.Pages.Components.Exceptions;
using Dnn.PersonaBar.Pages.Services.Dto;
using Dnn.PersonaBar.Themes.Components;
using DotNetNuke.Common.Utilities;
using DotNetNuke.Entities.Host;
using DotNetNuke.Entities.Portals;
using DotNetNuke.Entities.Tabs;
using DotNetNuke.Entities.Urls;
using DotNetNuke.Services.OutputCache;
using DotNetNuke.Web.Api;
using Dnn.PersonaBar.Library.Attributes;
using Dnn.PersonaBar.Library.DTO.Tabs;
using Dnn.PersonaBar.Pages.Components.Security;
using DotNetNuke.Common;
using DotNetNuke.Entities.Modules;
using DotNetNuke.Entities.Tabs.TabVersions;
using DotNetNuke.Entities.Users;
using DotNetNuke.Instrumentation;
using DotNetNuke.Security.Permissions;
using DotNetNuke.Services.Localization;
using DotNetNuke.Services.Social.Notifications;
using Localization = Dnn.PersonaBar.Pages.Components.Localization;

namespace Dnn.PersonaBar.Pages.Services
{
    [MenuPermission(MenuName = "Pages")]
    [DnnExceptionFilter]
    public class PagesController : PersonaBarApiController
    {
        private static readonly ILog Logger = LoggerSource.Instance.GetLogger(typeof(PagesController));
        private const string LocalResourceFile = "~/DesktopModules/admin/Dnn.PersonaBar/App_LocalResources/Pages.resx";

        private readonly IPagesController _pagesController;
        private readonly IBulkPagesController _bulkPagesController;
        private readonly IThemesController _themesController;
        private readonly ITemplateController _templateController;

        private readonly ITabController _tabController;
        private readonly ILocaleController _localeController;
        private readonly ISecurityService _securityService;

        public PagesController()
        {
            _pagesController = Components.PagesController.Instance;
            _themesController = ThemesController.Instance;
            _bulkPagesController = BulkPagesController.Instance;
            _templateController = TemplateController.Instance;

            _tabController = TabController.Instance;
            _localeController = LocaleController.Instance;
            _securityService = SecurityService.Instance;
        }

        /// GET: api/Pages/GetPageDetails
        /// <summary>
        /// Get detail of a page
        /// </summary>
        /// <param name="pageId"></param>
        /// <returns></returns>
        [HttpGet]
        public HttpResponseMessage GetPageDetails(int pageId)
        {
            if (!_securityService.CanManagePage(GetTabById(pageId)))
            {
                return GetForbiddenResponse();
            }

            try
            {
                var page = _pagesController.GetPageSettings(pageId);
                return Request.CreateResponse(HttpStatusCode.OK, page);
            }
            catch (PageNotFoundException)
            {
                return Request.CreateResponse(HttpStatusCode.NotFound, new {Message = "Page doesn't exists."});
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public HttpResponseMessage CreateCustomUrl(SeoUrl dto)
        {
            if (!_securityService.CanManagePage(GetTabById(dto.TabId)))
            {
                return GetForbiddenResponse();
            }

            var result = _pagesController.CreateCustomUrl(dto);

            return Request.CreateResponse(HttpStatusCode.OK,
                                new
                                {
                                    result.Id, result.Success, result.ErrorMessage, result.SuggestedUrlPath
                                });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public HttpResponseMessage UpdateCustomUrl(SeoUrl dto)
        {
            if (!_securityService.CanManagePage(GetTabById(dto.TabId)))
            {
                return GetForbiddenResponse();
            }

            var result = _pagesController.UpdateCustomUrl(dto);
            
            return Request.CreateResponse(HttpStatusCode.OK, new
            {
                result.Id,
                result.Success,
                result.ErrorMessage,
                result.SuggestedUrlPath
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public HttpResponseMessage DeleteCustomUrl(UrlIdDto dto)
        {
            if (!_securityService.CanManagePage(GetTabById(dto.TabId)))
            {
                return GetForbiddenResponse();
            }

            _pagesController.DeleteCustomUrl(dto);

            var response = new
            {
                Success = true
            };

            return Request.CreateResponse(HttpStatusCode.OK, response);
        }

        /// GET: api/Pages/GetPageList
        /// <summary>
        /// 
        /// </summary>
        /// <param name="parentId"></param>
        /// <param name="searchKey"></param>
        /// <returns></returns>
        [HttpGet]
        public HttpResponseMessage GetPageList(int parentId = -1, string searchKey = "")
        {
            if (!_securityService.IsPageAdminUser())
            {
                return GetForbiddenResponse();
            }

            var adminTabId = PortalSettings.AdminTabId;
            var tabs = TabController.GetPortalTabs(PortalSettings.PortalId, adminTabId, false, true, false, true);
            var pages = from p in _pagesController.GetPageList(parentId, searchKey)
                select Converters.ConvertToPageItem<PageItem>(p, tabs);
            return Request.CreateResponse(HttpStatusCode.OK, pages);
        }

        [HttpGet]
        public HttpResponseMessage GetPageHierarchy(int pageId)
        {
            if (!_securityService.IsPageAdminUser())
            {
                return GetForbiddenResponse();
            }

            try
            {
                var paths = _pagesController.GetPageHierarchy(pageId);
                return Request.CreateResponse(HttpStatusCode.OK, paths);
            }
            catch (PageNotFoundException)
            {
                return Request.CreateResponse(HttpStatusCode.NotFound);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public HttpResponseMessage MovePage(PageMoveRequest request)
        {
            if (!_securityService.IsPageAdminUser())
            {
                return GetForbiddenResponse();
            }

            try
            {
                var tab = _pagesController.MovePage(request);
                var tabs = TabController.GetPortalTabs(PortalSettings.PortalId, Null.NullInteger, false, true, false,
                    true);
                var pageItem = Converters.ConvertToPageItem<PageItem>(tab, tabs);
                return Request.CreateResponse(HttpStatusCode.OK, new {Status = 0, Page = pageItem});
            }
            catch (PageNotFoundException)
            {
                return Request.CreateResponse(HttpStatusCode.NotFound);
            }
            catch (PageException ex)
            {
                return Request.CreateResponse(HttpStatusCode.OK, new {Status = 1, ex.Message});
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public HttpResponseMessage DeletePage(PageItem page)
        {
            if (!_securityService.CanDeletePage(GetTabById(page.Id)))
            {
                return GetForbiddenResponse();
            }

            try
            {
                _pagesController.DeletePage(page);
                return Request.CreateResponse(HttpStatusCode.OK, new {Status = 0});
            }
            catch (PageNotFoundException)
            {

                return Request.CreateResponse(HttpStatusCode.NotFound);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public HttpResponseMessage DeletePageModule(PageModuleItem module)
        {
            if (!_securityService.CanManagePage(GetTabById(module.PageId)))
            {
                return GetForbiddenResponse();
            }

            try
            {
                _pagesController.DeleteTabModule(module.PageId, module.ModuleId);
                return Request.CreateResponse(HttpStatusCode.OK, new {Status = 0});
            }
            catch (PageModuleNotFoundException)
            {
                return Request.CreateResponse(HttpStatusCode.NotFound);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public HttpResponseMessage CopyThemeToDescendantPages(CopyThemeRequest copyTheme)
        {
            if (!_securityService.CanManagePage(GetTabById(copyTheme.PageId)))
            {
                return GetForbiddenResponse();
            }

            _pagesController.CopyThemeToDescendantPages(copyTheme.PageId, copyTheme.Theme);
            return Request.CreateResponse(HttpStatusCode.OK, new {Status = 0});
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public HttpResponseMessage CopyPermissionsToDescendantPages(CopyPermissionsRequest copyPermissions)
        {
            if (!_securityService.CanAdminPage(GetTabById(copyPermissions.PageId)))
            {
                return GetForbiddenResponse();
            }

            try
            {
                _pagesController.CopyPermissionsToDescendantPages(copyPermissions.PageId);
                return Request.CreateResponse(HttpStatusCode.OK, new {Status = 0});
            }
            catch (PageNotFoundException)
            {
                return Request.CreateResponse(HttpStatusCode.NotFound);
            }
            catch (PermissionsNotMetException)
            {
                return Request.CreateResponse(HttpStatusCode.Forbidden);
            }
        }

        // TODO: This should be a POST
        [HttpGet]
        public HttpResponseMessage EditModeForPage(int id)
        {
            if (!_securityService.CanManagePage(GetTabById(id)))
            {
                return GetForbiddenResponse();
            }

            _pagesController.EditModeForPage(id, UserInfo.UserID);
            return Request.CreateResponse(HttpStatusCode.OK);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public HttpResponseMessage SavePageDetails(PageSettings pageSettings)
        {
            if (!CanSavePageDetails(pageSettings))
            {
                return GetForbiddenResponse();
            }

            try
            {
                var tab = _pagesController.SavePageDetails(pageSettings);
                var tabs = TabController.GetPortalTabs(PortalSettings.PortalId, Null.NullInteger, false, true, false,
                    true);

                return Request.CreateResponse(HttpStatusCode.OK, new
                {
                    Status = 0,
                    Page = Converters.ConvertToPageItem<PageItem>(tab, tabs)
                });
            }
            catch (PageNotFoundException)
            {
                return Request.CreateResponse(HttpStatusCode.NotFound, new {Message = "Page doesn't exists."});
            }
            catch (PageValidationException ex)
            {
                return Request.CreateResponse(HttpStatusCode.OK, new {Status = 1, ex.Field, ex.Message});
            }
        }

        [HttpGet]
        public HttpResponseMessage GetDefaultSettings()
        {
            var settings = _pagesController.GetDefaultSettings();
            return Request.CreateResponse(HttpStatusCode.OK, settings);
        }

        [HttpGet]
        public HttpResponseMessage GetCacheProviderList()
        {
            var providers = from p in OutputCachingProvider.GetProviderList() select p.Key;
            return Request.CreateResponse(HttpStatusCode.OK, providers);
        }

        [HttpGet]
        public HttpResponseMessage GetPageUrlPreview(string url)
        {
            var cleanedUrl = _pagesController.CleanTabUrl(url);
            return Request.CreateResponse(HttpStatusCode.OK, new {Url = cleanedUrl});
        }

        [HttpGet]
        public HttpResponseMessage GetThemes()
        {
            var themes = _themesController.GetLayouts(PortalSettings, ThemeLevel.Global | ThemeLevel.Site);
            var defaultPortalThemeName = GetDefaultPortalTheme();
            var defaultPortalLayout = GetDefaultPortalLayout();
            var defaultPortalContainer = GetDefaultPortalContainer();

            return Request.CreateResponse(HttpStatusCode.OK, new
            {
                themes,
                defaultPortalThemeName,
                defaultPortalLayout,
                defaultPortalContainer
            });
        }

        [HttpGet]
        public HttpResponseMessage GetThemeFiles(string themeName)
        {
            const ThemeLevel level = ThemeLevel.Global | ThemeLevel.Site;
            var themeLayout = _themesController.GetLayouts(PortalSettings, level).FirstOrDefault(t => t.PackageName.Equals(themeName, StringComparison.InvariantCultureIgnoreCase));
            var themeContainer = _themesController.GetContainers(PortalSettings, level).FirstOrDefault(t => t.PackageName.Equals(themeName, StringComparison.InvariantCultureIgnoreCase));

            if (themeLayout == null || themeContainer == null)
            {
                return Request.CreateErrorResponse(HttpStatusCode.NotFound, "ThemeNotFound");
            }

            return Request.CreateResponse(HttpStatusCode.OK, new {
                layouts = _themesController.GetThemeFiles(PortalSettings, themeLayout),
                containers = _themesController.GetThemeFiles(PortalSettings, themeContainer)
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public HttpResponseMessage SaveBulkPages(BulkPage bulkPage)
        {
            if (!_securityService.IsPageAdminUser())
            {
                return GetForbiddenResponse();
            }

            try
            {
                var bulkPageResponse = _bulkPagesController.AddBulkPages(bulkPage);

                return Request.CreateResponse(HttpStatusCode.OK, new
                {
                    Status = 0,
                    Response = bulkPageResponse
                });
            }
            catch (PageValidationException ex)
            {
                return Request.CreateResponse(HttpStatusCode.OK, new { Status = 1, ex.Field, ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public HttpResponseMessage SavePageAsTemplate(PageTemplate pageTemplate)
        {
            if (!_securityService.CanExportPage(GetTabById(pageTemplate.TabId)))
            {
                return GetForbiddenResponse();
            }

            try
            {
                var templateFilename = _templateController.SaveAsTemplate(pageTemplate);
                var response = string.Format(Localization.GetString("ExportedMessage"), templateFilename);

                return Request.CreateResponse(HttpStatusCode.OK, new
                {
                    Status = 0,
                    Response = response
                });
            }
            catch (TemplateException ex)
            {
                return Request.CreateResponse(HttpStatusCode.OK, new { Status = 1, ex.Message });
            }
        }

        #region -------------------------------- LOCALIZATION API METHODS SEPARATOR --------------------------------
        // From inside Visual Studio editor press [CTRL]+[M] then [O] to collapse source code to definition
        // From inside Visual Studio editor press [CTRL]+[M] then [P] to expand source code folding
        #endregion

        // POST /api/personabar/pages/MakePageNeutral?tabId=123
        [HttpPost]
        [ValidateAntiForgeryToken]
        public HttpResponseMessage MakePageNeutral([FromUri] int tabId)
        {
            try
            {
                if (_tabController.GetTabsByPortal(PortalId).WithParentId(tabId).Count > 0)
                {
                    return Request.CreateErrorResponse(HttpStatusCode.BadRequest, LocalizeString("MakeNeutral.ErrorMessage"));
                }

                var defaultLocale = _localeController.GetDefaultLocale(PortalId);
                _tabController.ConvertTabToNeutralLanguage(PortalId, tabId, defaultLocale.Code, true);
                return Request.CreateResponse(HttpStatusCode.OK, new { Success = true });
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex.ToString());
            }
        }

        // POST /api/personabar/pages/MakePageTranslatable?tabId=123
        [HttpPost]
        [ValidateAntiForgeryToken]
        public HttpResponseMessage MakePageTranslatable([FromUri] int tabId)
        {
            try
            {
                var currentTab = GetTabById(tabId);
                if (currentTab == null)
                {
                    return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "InvalidTab");
                }

                var defaultLocale = _localeController.GetDefaultLocale(PortalId);
                _tabController.LocalizeTab(currentTab, defaultLocale, false);
                _tabController.AddMissingLanguages(PortalId, tabId);
                _tabController.ClearCache(PortalId);
                return Request.CreateResponse(HttpStatusCode.OK, new { Success = true });
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex.ToString());
            }
        }

        // POST /api/personabar/pages/AddMissingLanguages?tabId=123
        [HttpPost]
        [ValidateAntiForgeryToken]
        public HttpResponseMessage AddMissingLanguages([FromUri] int tabId)
        {
            try
            {
                _tabController.AddMissingLanguages(PortalId, tabId);
                _tabController.ClearCache(PortalId);
                return Request.CreateResponse(HttpStatusCode.OK, new { Success = true });
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex.ToString());
            }
        }

        // POST /api/personabar/pages/NotifyTranslators
        [HttpPost]
        [ValidateAntiForgeryToken]
        public HttpResponseMessage NotifyTranslators(TranslatorsComment comment)
        {
            try
            {
                // loop through all localized version of this page
                var currentTab = GetTabById(comment.TabId);
                foreach (var localizedTab in currentTab.LocalizedTabs.Values)
                {
                    var users = new Dictionary<int, UserInfo>();

                    //Give default translators for this language and administrators permissions
                    _tabController.GiveTranslatorRoleEditRights(localizedTab, users);

                    //Send Messages to all the translators of new content
                    foreach (var translator in users.Values.Where(user => user.UserID != PortalSettings.AdministratorId))
                    {
                        AddTranslationSubmittedNotification(localizedTab, translator, comment.Text);
                    }
                }

                var msgToUser = LocalizeString("TranslationMessageConfirmMessage.Text");
                return Request.CreateResponse(HttpStatusCode.OK, new { Success = true, Message = msgToUser });
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex.ToString());
            }
        }

        // GET /api/personabar/pages/GetTabLocalization?tabId=123
        /// <summary>
        /// Gets the view data that used to be in the old ControlBar's localization tab
        /// under Page Settings ( /{page}/ctl/Tab/action/edit/activeTab/settingTab ).
        /// </summary>
        /// <param name="tabId">The ID of the tab to get localization for.</param>
        /// <returns></returns>
        [HttpGet]
        public HttpResponseMessage GetTabLocalization(int tabId)
        {
            try
            {
                var pages = GetNonLocalizedPages(tabId);
                return Request.CreateResponse(HttpStatusCode.OK, pages);
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex.ToString());
            }
        }

        // POST /api/personabar/pages/UpdateTabLocalization
        [HttpPost]
        [ValidateAntiForgeryToken]
        public HttpResponseMessage UpdateTabLocalization(DnnPagesDto pages)
        {
            try
            {
                SaveNonLocalizedPages(pages);
                return Request.CreateResponse(HttpStatusCode.OK, new { Success = true });
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex.ToString());
            }
        }

        // POST /api/personabar/pages/RestoreModule?tabModuleId=123
        [HttpPost]
        [ValidateAntiForgeryToken]
        public HttpResponseMessage RestoreModule(int tabModuleId)
        {
            try
            {
                var moduleController = ModuleController.Instance;
                var moduleInfo = moduleController.GetTabModule(tabModuleId);
                if (moduleInfo == null)
                {
                    return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "InvalidTabModule");
                }

                moduleController.RestoreModule(moduleInfo);
                return Request.CreateResponse(HttpStatusCode.OK, new { Success = true });
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex.ToString());
            }
        }

        // POST /api/personabar/pages/DeleteModule?tabModuleId=123
        [HttpPost]
        [ValidateAntiForgeryToken]
        public HttpResponseMessage DeleteModule(int tabModuleId)
        {
            try
            {
                var moduleController = ModuleController.Instance;
                var moduleInfo = moduleController.GetTabModule(tabModuleId);
                if (moduleInfo == null)
                {
                    return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "InvalidTabModule");
                }

                moduleController.DeleteTabModule(moduleInfo.TabID, moduleInfo.ModuleID, false);
                return Request.CreateResponse(HttpStatusCode.OK, new { Success = true });
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex.ToString());
            }
        }

        #region -------------------------------- PRIVATE METHODS SEPARATOR --------------------------------
        // From inside Visual Studio editor press [CTRL]+[M] then [O] to collapse source code to definition
        // From inside Visual Studio editor press [CTRL]+[M] then [P] to expand source code folding
        #endregion

        private static string LocalizeString(string key)
        {
            return DotNetNuke.Services.Localization.Localization.GetString(key, LocalResourceFile);
        }

        //private bool IsDefaultLanguage(string cultureCode)
        //{
        //    return string.Equals(cultureCode, PortalSettings.DefaultLanguage, StringComparison.InvariantCultureIgnoreCase);
        //}

        //private bool IsLanguageEnabled(string cultureCode)
        //{
        //    return _localeController.GetLocales(PortalId).ContainsKey(cultureCode);
        //}

        private DnnPagesDto GetNonLocalizedPages(int tabId)
        {
            var currentTab = GetTabById(tabId);

            //Unique id of default language page
            var uniqueId = currentTab.DefaultLanguageGuid != Null.NullGuid
                ? currentTab.DefaultLanguageGuid
                : currentTab.UniqueId;

            // get all non admin pages and not deleted
            var allPages = _tabController.GetTabsByPortal(PortalId).Values.Where(
                t => t.TabID != PortalSettings.AdminTabId && (Null.IsNull(t.ParentId) || t.ParentId != PortalSettings.AdminTabId));
            allPages = allPages.Where(t => t.IsDeleted == false);
            // get all localized pages of current page
            var tabInfos = allPages as IList<TabInfo> ?? allPages.ToList();
            var localizedPages = tabInfos.Where(
                t => t.DefaultLanguageGuid == uniqueId || t.UniqueId == uniqueId).OrderBy(t => t.DefaultLanguageGuid).ToList();
            Dictionary<string, TabInfo> localizedTabs = null;

            // we are going to build up a list of locales
            // this is a bit more involved, since we want the default language to be first.
            // also, we do not want to add any locales the user has no access to
            var locales = new List<LocaleInfoDto>();
            var localeController = new LocaleController();
            var localeDict = localeController.GetLocales(PortalId);
            if (localeDict.Count == 0)
            {
                locales.Add(new LocaleInfoDto(""));
            }
            else
            {
                if (localizedPages.Count == 1 && localizedPages.First().CultureCode == "")
                {
                    // locale neutral page
                    locales.Add(new LocaleInfoDto(""));
                }
                else if (localizedPages.Count == 1 && localizedPages.First().CultureCode != PortalSettings.DefaultLanguage)
                {
                    var first = localizedPages.First();
                    locales.Add(new LocaleInfoDto(first.CultureCode));
                    //localizedTabs = new Dictionary<string, TabInfo> { { first.CultureCode, first } };
                }
                else
                {
                    //force sort order, so first add default language
                    locales.Add(new LocaleInfoDto(PortalSettings.DefaultLanguage));

                    // build up a list of localized tabs.
                    // depending on whether or not the selected page is in the default langauge
                    // we will add the localized tabs from the current page
                    // or from the defaultlanguage page
                    if (currentTab.CultureCode == PortalSettings.DefaultLanguage)
                    {
                        localizedTabs = currentTab.LocalizedTabs;
                    }
                    else
                    {
                        // selected page is not in default language
                        // add localizedtabs from defaultlanguage page
                        if (currentTab.DefaultLanguageTab != null)
                        {
                            localizedTabs = currentTab.DefaultLanguageTab.LocalizedTabs;
                        }
                    }

                    if (localizedTabs != null)
                    {
                        // only add locales from tabs the user has at least view permissions to.
                        // we will handle the edit permissions at a later stage
                        locales.AddRange(
                            from localizedTab in localizedTabs
                            where TabPermissionController.CanViewPage(localizedTab.Value)
                            select new LocaleInfoDto(localizedTab.Value.CultureCode));
                    }
                }
            }

            var dnnPages = new DnnPagesDto(locales)
            {
                HasMissingLanguages = _tabController.HasMissingLanguages(PortalId, tabId)
            };

            // filter the list of localized pages to only those that have a culture we want to see
            var viewableLocalizedPages = localizedPages.Where(
                localizedPage => locales.Find(locale => locale.CultureCode == localizedPage.CultureCode) != null).ToList();

            foreach (var tabInfo in viewableLocalizedPages)
            {
                var localTabInfo = tabInfo;
                var dnnPage = dnnPages.Page(localTabInfo.CultureCode);
                if (!TabPermissionController.CanViewPage(tabInfo))
                {
                    dnnPages.RemoveLocale(localTabInfo.CultureCode);
                    dnnPages.Pages.Remove(dnnPage);
                    break;
                }
                dnnPage.TabId = localTabInfo.TabID;
                dnnPage.TabName = localTabInfo.TabName;
                dnnPage.Title = localTabInfo.Title;
                dnnPage.Description = localTabInfo.Description;
                dnnPage.Path = localTabInfo.TabPath.Substring(0, localTabInfo.TabPath.LastIndexOf("//", StringComparison.Ordinal)).Replace("//", "");
                dnnPage.HasChildren = (_tabController.GetTabsByPortal(PortalId).WithParentId(tabInfo.TabID).Count != 0);
                dnnPage.CanAdminPage = TabPermissionController.CanAdminPage(tabInfo);
                dnnPage.CanViewPage = TabPermissionController.CanViewPage(tabInfo);
                dnnPage.LocalResourceFile = LocalResourceFile;
                dnnPage.PageUrl = Globals.NavigateURL(localTabInfo.TabID, false, PortalSettings, "", localTabInfo.CultureCode);

                // calculate position in the form of 1.3.2...
                var siblingTabs = tabInfos.Where(t => t.ParentId == localTabInfo.ParentId && t.CultureCode == localTabInfo.CultureCode || t.CultureCode == null).OrderBy(t => t.TabOrder).ToList();
                dnnPage.Position = (siblingTabs.IndexOf(localTabInfo) + 1).ToString(CultureInfo.InvariantCulture);
                var parentTabId = localTabInfo.ParentId;
                while (parentTabId > 0)
                {
                    var parentTab = tabInfos.Single(t => t.TabID == parentTabId);
                    var id = parentTabId;
                    siblingTabs = tabInfos.Where(t => t.ParentId == id && t.CultureCode == localTabInfo.CultureCode || t.CultureCode == null).OrderBy(t => t.TabOrder).ToList();
                    dnnPage.Position = (siblingTabs.IndexOf(localTabInfo) + 1).ToString(CultureInfo.InvariantCulture) + "." + dnnPage.Position;
                    parentTabId = parentTab.ParentId;
                }

                dnnPage.DefaultLanguageGuid = localTabInfo.DefaultLanguageGuid;
                dnnPage.IsTranslated = localTabInfo.IsTranslated;
                dnnPage.IsPublished = _tabController.IsTabPublished(localTabInfo);
                // generate modules information
                var moduleController = ModuleController.Instance;
                foreach (var moduleInfo in moduleController.GetTabModules(localTabInfo.TabID).Values)
                {
                    var guid = moduleInfo.DefaultLanguageGuid == Null.NullGuid ? moduleInfo.UniqueId : moduleInfo.DefaultLanguageGuid;

                    var dnnModules = dnnPages.Module(guid); // modules of each language
                    var dnnModule = dnnModules.Module(localTabInfo.CultureCode);
                    // detect error : 2 modules with same uniqueId on the same page
                    dnnModule.LocalResourceFile = LocalResourceFile;
                    if (dnnModule.TabModuleId > 0)
                    {
                        dnnModule.ErrorDuplicateModule = true;
                        dnnPages.ErrorExists = true;
                        continue;
                    }

                    dnnModule.ModuleTitle = moduleInfo.ModuleTitle;
                    dnnModule.DefaultLanguageGuid = moduleInfo.DefaultLanguageGuid;
                    dnnModule.TabId = localTabInfo.TabID;
                    dnnModule.TabModuleId = moduleInfo.TabModuleID;
                    dnnModule.ModuleId = moduleInfo.ModuleID;
                    dnnModule.CanAdminModule = ModulePermissionController.CanAdminModule(moduleInfo);
                    dnnModule.CanViewModule = ModulePermissionController.CanViewModule(moduleInfo);
                    dnnModule.IsDeleted = moduleInfo.IsDeleted;
                    if (moduleInfo.DefaultLanguageGuid != Null.NullGuid)
                    {
                        var defaultLanguageModule = moduleController.GetModuleByUniqueID(moduleInfo.DefaultLanguageGuid);
                        if (defaultLanguageModule != null)
                        {
                            dnnModule.DefaultModuleId = defaultLanguageModule.ModuleID;
                            if (defaultLanguageModule.ParentTab.UniqueId != moduleInfo.ParentTab.DefaultLanguageGuid)
                                dnnModule.DefaultTabName = defaultLanguageModule.ParentTab.TabName;
                        }
                    }
                    dnnModule.SetModuleInfoHelp();
                    dnnModule.IsTranslated = moduleInfo.IsTranslated;
                    dnnModule.IsLocalized = moduleInfo.IsLocalized;

                    dnnModule.IsShared = _tabController.GetTabsByModuleID(moduleInfo.ModuleID).Values.Count(t => t.CultureCode == moduleInfo.CultureCode) > 1;

                    // detect error : the default language module is on an other page
                    dnnModule.ErrorDefaultOnOtherTab = moduleInfo.DefaultLanguageGuid != Null.NullGuid && moduleInfo.DefaultLanguageModule == null;

                    // detect error : different culture on tab and module
                    dnnModule.ErrorCultureOfModuleNotCultureOfTab = moduleInfo.CultureCode != localTabInfo.CultureCode;

                    dnnPages.ErrorExists |= dnnModule.ErrorDefaultOnOtherTab || dnnModule.ErrorCultureOfModuleNotCultureOfTab;
                }
            }

            return dnnPages;
        }

        private void SaveNonLocalizedPages(DnnPagesDto pages)
        {
            // check all pages
            foreach (var page in pages.Pages)
            {
                var tabInfo = _tabController.GetTab(page.TabId, PortalId, true);
                if (tabInfo != null &&
                    (tabInfo.TabName != page.TabName ||
                     tabInfo.Title != page.Title ||
                     tabInfo.Description != page.Description))
                {
                    tabInfo.TabName = page.TabName;
                    tabInfo.Title = page.Title;
                    tabInfo.Description = page.Description;
                    _tabController.UpdateTab(tabInfo);
                }
            }

            var tabsToPublish = new List<TabInfo>();
            var moduleTranslateOverrides = new Dictionary<int, bool>();
            var moduleController = ModuleController.Instance;

            // manage all actions we need to take for all modules on all pages
            foreach (var modulesCollection in pages.Modules)
            {
                foreach (var moduleDto in modulesCollection.Modules)
                {
                    var tabModule = moduleController.GetTabModule(moduleDto.TabModuleId);
                    if (tabModule != null)
                    {
                        if (tabModule.ModuleTitle != moduleDto.ModuleTitle)
                        {
                            tabModule.ModuleTitle = moduleDto.ModuleTitle;
                            moduleController.UpdateModule(tabModule);
                        }

                        if (tabModule.DefaultLanguageGuid != Null.NullGuid &&
                            tabModule.IsLocalized != moduleDto.IsLocalized)
                        {
                            var locale = _localeController.GetLocale(tabModule.CultureCode);
                            if (moduleDto.IsLocalized)
                                moduleController.LocalizeModule(tabModule, locale);
                            else
                                moduleController.DeLocalizeModule(tabModule);
                        }

                        bool moduleTranslateOverride;
                        moduleTranslateOverrides.TryGetValue(tabModule.TabID, out moduleTranslateOverride);

                        if (!moduleTranslateOverride && tabModule.IsTranslated != moduleDto.IsTranslated)
                        {
                            moduleController.UpdateTranslationStatus(tabModule, moduleDto.IsTranslated);
                        }
                    }
                    else if (moduleDto.CopyModule)
                    {
                        // find the first existing module on the line
                        foreach (var moduleToCopyFrom in modulesCollection.Modules)
                        {
                            if (moduleToCopyFrom.ModuleId > 0)
                            {
                                var miCopy = moduleController.GetTabModule(moduleToCopyFrom.ModuleId);
                                if (miCopy.DefaultLanguageGuid == Null.NullGuid)
                                {
                                    // default
                                    var toTabInfo = _tabController.GetTab(moduleToCopyFrom.TabId, PortalSettings.PortalId, false);
                                    DisableTabVersioningAndWorkflow(toTabInfo);
                                    moduleController.CopyModule(miCopy, toTabInfo, Null.NullString, true);
                                    EnableTabVersioningAndWorkflow(toTabInfo);
                                    var localizedModule = moduleController.GetModule(miCopy.ModuleID, moduleToCopyFrom.TabId, false);
                                    moduleController.LocalizeModule(localizedModule, LocaleController.Instance.GetLocale(localizedModule.CultureCode));
                                }
                                else
                                {
                                    var miCopyDefault = moduleController.GetModuleByUniqueID(miCopy.DefaultLanguageGuid);
                                    var toTabInfo = _tabController.GetTab(moduleToCopyFrom.TabId, PortalSettings.PortalId, false);
                                    moduleController.CopyModule(miCopyDefault, toTabInfo, Null.NullString, true);
                                }

                                if (moduleDto == modulesCollection.Modules.First())
                                {
                                    // default language
                                    var miDefault = moduleController.GetModule(miCopy.ModuleID, pages.Pages.First().TabId, false);
                                    foreach (var page in pages.Pages.Skip(1))
                                    {
                                        var moduleInfo = moduleController.GetModule(miCopy.ModuleID, page.TabId, false);
                                        if (moduleInfo != null)
                                        {
                                            if (miDefault != null)
                                            {
                                                moduleInfo.DefaultLanguageGuid = miDefault.UniqueId;
                                            }
                                            moduleController.UpdateModule(moduleInfo);
                                        }
                                    }
                                }

                                break;
                            }
                        }
                    }
                }
            }

            foreach (var page in pages.Pages)
            {
                var tabInfo = _tabController.GetTab(page.TabId, PortalId, true);
                if (tabInfo != null)
                {
                    var moduleTranslateOverride = false;
                    if (!tabInfo.IsDefaultLanguage)
                    {
                        if (tabInfo.IsTranslated != page.IsTranslated)
                        {
                            _tabController.UpdateTranslationStatus(tabInfo, page.IsTranslated);
                            if (page.IsTranslated)
                            {
                                moduleTranslateOverride = true;
                                var tabModules = moduleController.GetTabModules(tabInfo.TabID)
                                    .Where(moduleKvp =>
                                        moduleKvp.Value.DefaultLanguageModule != null &&
                                        moduleKvp.Value.LocalizedVersionGuid != moduleKvp.Value.DefaultLanguageModule.LocalizedVersionGuid);

                                foreach (var moduleKvp in tabModules)
                                {
                                    moduleController.UpdateTranslationStatus(moduleKvp.Value, true);
                                }
                            }
                        }

                        if (page.IsPublished)
                        {
                            tabsToPublish.Add(tabInfo);
                        }
                    }

                    moduleTranslateOverrides.Add(page.TabId, moduleTranslateOverride);
                }
            }

            // if we have tabs to publish, do it.
            // marks all modules as translated, marks page as translated
            foreach (var tabInfo in tabsToPublish)
            {
                //First mark all modules as translated
                foreach (var module in moduleController.GetTabModules(tabInfo.TabID).Values)
                {
                    moduleController.UpdateTranslationStatus(module, true);
                }

                //Second mark tab as translated
                _tabController.UpdateTranslationStatus(tabInfo, true);

                //Third publish Tab (update Permissions)
                _tabController.PublishTab(tabInfo);
            }

            // manage translated status of tab. In order to do that, we need to check if all modules on the page are translated
            var tabTranslatedStatus = true;
            foreach (var page in pages.Pages)
            {
                var tabInfo = _tabController.GetTab(page.TabId, PortalId, true);
                if (tabInfo != null)
                {
                    if (tabInfo.ChildModules.Any(moduleKvp => !moduleKvp.Value.IsTranslated))
                    {
                        tabTranslatedStatus = false;
                    }

                    if (tabTranslatedStatus && !tabInfo.IsTranslated)
                    {
                        _tabController.UpdateTranslationStatus(tabInfo, true);
                    }
                }
            }
        }

        private static void EnableTabVersioningAndWorkflow(TabInfo tab)
        {
            var tabVersionSettings = TabVersionSettings.Instance;
            var tabWorkflowSettings = TabWorkflowSettings.Instance;

            if (tabVersionSettings.IsVersioningEnabled(tab.PortalID))
            {
                tabVersionSettings.SetEnabledVersioningForTab(tab.TabID, true);
            }

            if (tabWorkflowSettings.IsWorkflowEnabled(tab.PortalID))
            {
                tabWorkflowSettings.SetWorkflowEnabled(tab.PortalID, tab.TabID, true);
            }
        }

        private static void DisableTabVersioningAndWorkflow(TabInfo tab)
        {
            var tabVersionSettings = TabVersionSettings.Instance;
            var tabWorkflowSettings = TabWorkflowSettings.Instance;

            if (tabVersionSettings.IsVersioningEnabled(tab.PortalID))
            {
                tabVersionSettings.SetEnabledVersioningForTab(tab.TabID, false);
            }

            if (tabWorkflowSettings.IsWorkflowEnabled(tab.PortalID))
            {
                tabWorkflowSettings.SetWorkflowEnabled(tab.PortalID, tab.TabID, false);
            }
        }

        private void AddTranslationSubmittedNotification(TabInfo tabInfo, UserInfo translator, string comment)
        {
            var notificationsController = NotificationsController.Instance;
            var notificationType = notificationsController.GetNotificationType("TranslationSubmitted");
            var subject = LocalizeString("NewContentMessage.Subject");
            var body = string.Format(LocalizeString("NewContentMessage.Body"),
                tabInfo.TabName,
                Globals.NavigateURL(tabInfo.TabID, false, PortalSettings, Null.NullString, tabInfo.CultureCode),
                comment);

            var sender = UserController.GetUserById(PortalSettings.PortalId, PortalSettings.AdministratorId);
            var notification = new Notification
            {
                NotificationTypeID = notificationType.NotificationTypeId,
                Subject = subject,
                Body = body,
                IncludeDismissAction = true,
                SenderUserID = sender.UserID
            };

            notificationsController.SendNotification(notification, PortalSettings.PortalId, null, new List<UserInfo> { translator });
        }

        private HttpResponseMessage GetForbiddenResponse()
        {
            return Request.CreateResponse(HttpStatusCode.Forbidden, new { Message = "The user is not allowed to access this method." });
        }

        private string GetDefaultPortalTheme()
        {
            var layoutSrc = GetDefaultPortalLayout();
            if (string.IsNullOrWhiteSpace(layoutSrc))
            {
                return null;
            }
            var layout = _themesController.GetThemeFile(PortalSettings.Current, layoutSrc, ThemeType.Skin);
            return layout?.ThemeName;
        }

        private string GetDefaultPortalContainer()
        {
            return PortalController.GetPortalSetting("DefaultPortalContainer", PortalId, Host.DefaultPortalSkin, PortalSettings.CultureCode);
        }

        private string GetDefaultPortalLayout()
        {
            return PortalController.GetPortalSetting("DefaultPortalSkin", PortalId, Host.DefaultPortalSkin, PortalSettings.CultureCode);
        }

        private TabInfo GetTabById(int pageId)
        {
            return pageId <= 0 ? null : _tabController.GetTab(pageId, PortalId, false);
        }

        private bool CanSavePageDetails(PageSettings pageSettings)
        {
            var tabId = pageSettings.TabId;
            var parentId = pageSettings.ParentId ?? 0;
            var creatingPage = parentId > 0 && tabId <= 0 && pageSettings.TemplateId <= 0;
            var creatingTemplate = tabId <= 0 && pageSettings.TemplateId > 0;
            var updatingPage = tabId > 0;
            var duplicatingPage = tabId <= 0 && pageSettings.TemplateTabId > 0;

            return (
                _securityService.IsPageAdminUser() ||
                creatingPage && _securityService.CanAddPage(GetTabById(parentId)) ||
                creatingTemplate && _securityService.CanExportPage(GetTabById(pageSettings.TemplateId)) ||
                updatingPage && _securityService.CanManagePage(GetTabById(tabId)) ||
                duplicatingPage && _securityService.CanCopyPage(GetTabById(pageSettings.TemplateTabId))
            );
        }
    }
}
