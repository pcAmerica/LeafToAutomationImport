using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using dotNetExt;
using pcAmerica.Desktop.POS.ServiceModel.Operations;
using pcAmerica.Desktop.POS.ServiceModel.Operations.Menus;
using pcAmerica.Desktop.POS.ServiceModel.Types;
using pcAmerica.Desktop.POS.ServiceModel.Types.Payments;
using PowerArgs;
using Item = pcAmerica.Desktop.POS.ServiceModel.Types.Item;
using ModifierGroup = pcAmerica.Desktop.POS.ServiceModel.Types.ModifierGroup;
using Store = pcAmerica.Desktop.POS.ServiceModel.Types.Store;

namespace LeafToAutomationImport
{
    internal class Program
    {
        private struct Counters
        {
            public const string TaxRate = "TaxRate";
            public const string TaxGroup = "TaxGroup";
            public const string TaxGroupTaxRate = "TaxGroupTaxRate";
            public const string Tender = "Tender";
            public const string PaymentProfileTender = "PaymentProfileTender";
            public const string KitchenPrinter = "KitchenPrinter";
            public const string Jobcode = "Jobcode";
            public const string Employee = "Employee";
            public const string EmployeeEmail = "EmployeeEmail";
            public const string EmployeePhone = "EmployeePhone";
            public const string EmployeeJobcode = "EmployeeJobcode";
            public const string Department = "Department";
            public const string Modifier = "Modifier";
            public const string ModifierGroup = "ModifierGroup";
            public const string ModifierGroupMember = "ModifierGroupMember";
            public const string Item = "Item";
            public const string ItemBarcode = "ItemBarcode";
            public const string ItemModifierGroup = "ItemModifierGroup";
            public const string KitchenPrinterItemMapping = "KitchenPrinterItemMapping";
            public const string MenuPanel = "MenuPanel";
            public const string MenuButton = "MenuButton";
        }

        private class Mapping
        {
            public string LeafId;
            public Guid PosId;
        }

        private class ModGroupMapping
        {
            public string LeafId;
            public ModifierGroup PosModifierGroup;
        }

        private class ItemMapping
        {
            public string LeafId;
            public Item PosItem;
        }

        public class CommandLineArgs
        {
            [ArgRequired]
            public string LeafExportFullPath { get; set; }

            [ArgDefaultValue("http://localhost:52454")]
            public string ServerUrl { get; set; }

            [ArgDefaultValue("VSw9i0ujqf40Kx")]
            public string ApiKey { get; set; }
        }

        private static Dictionary<string, Guid> _taxMap;
        private static Dictionary<string, Guid> _printerMap;
        private static Dictionary<string, Guid> _jobCodeMap;
        private static Dictionary<string, Guid> _departmentMap;
        private static Dictionary<string, Guid> _modifierMap;
        private static Dictionary<string, ModifierGroup> _modifierGroupsMap;
        private static Dictionary<string, Item> _itemMap;

        private static Dictionary<string, int> _counters;

        private static void Main(string[] args)
        {
            try
            {
                var parsed = Args.Parse<CommandLineArgs>(args);

                _taxMap = new Dictionary<string, Guid>();
                _printerMap = new Dictionary<string, Guid>();
                _jobCodeMap = new Dictionary<string, Guid>();
                _departmentMap = new Dictionary<string, Guid>();
                _modifierMap = new Dictionary<string, Guid>();
                _modifierGroupsMap = new Dictionary<string, ModifierGroup>();
                _itemMap = new Dictionary<string, Item>();
                _counters = new Dictionary<string, int>();

                var stopwatch = new Stopwatch();
                stopwatch.Start();

                using (var reader = System.IO.File.OpenText(parsed.LeafExportFullPath))
                {
                    var leafStore = ServiceStack.Text.JsonSerializer.DeserializeFromReader<LeafDataModel.Store>(reader);

                    var api = new Api {Apikey = parsed.ApiKey, BaseUri = parsed.ServerUrl};

                    SetupStore(api, leafStore);
                    SetupTaxes(api, leafStore);
                    SetupTenders(api, leafStore);
                    SetupPrinters(api, leafStore);
                    SetupJobCodes(api, leafStore);
                    SetupUsers(api, leafStore);
                    SetupDepartments(api, leafStore);
                    SetupModifiers(api, leafStore);
                    SetupModifierGroups(api, leafStore);
                    SetupItems(api, leafStore);
                    SetupMenu(api, leafStore);
                }

                stopwatch.Stop();

                PrintResults();

                Log("");
                Log("Import complete, duration: {0:%h}h {0:%m}m {0:%s}s", stopwatch.Elapsed);
            }
            catch (ArgException ex)
            {
                Log(ex.Message);
                Log(ArgUsage.GenerateUsageFromTemplate<CommandLineArgs>().ToString());
            }
            catch (Exception ex)
            {
                Log(ex.ToString());
            }
        }

        private static void IncrementCounter(string counterName)
        {
            if (_counters.ContainsKey(counterName))
            {
                _counters[counterName] = _counters[counterName] + 1;
            }
            else
            {
                _counters[counterName] = 1;
            }
        }

        private static void Log(string message, params object[] args)
        {
            Console.WriteLine(message, args);
        }

        private static void PrintResults()
        {
            var counters = _counters.OrderBy(pair => pair.Key).ToList();

            Log("");
            Log("==============");
            Log("IMPORT RESULTS");
            Log("==============");

            counters.ForEach(pair => Log("{0} : {1}", pair.Key, pair.Value));
        }

        private static void SetupStore(Api api, LeafDataModel.Store leafStore)
        {
            var posStore = new Store
            {
                Name = leafStore.siteName,
                Email = leafStore.siteEmail,
                PhoneNumber = leafStore.sitePhone,
                UserFriendlyId = "100",
                Address = new StoreAddress
                {
                    City = leafStore.primary_address.city,
                    PostalCode = leafStore.primary_address.postalCode,
                    State = leafStore.primary_address.stateShort,
                    Street = leafStore.primary_address.address,
                    Street2 = leafStore.primary_address.address2,
                    Country = ""
                }
            };

            api.Post(posStore);

            Log("Created Store");
        }

        private static void SetupTaxes(Api api, LeafDataModel.Store leafStore)
        {
            if (leafStore.bevTax != 0)
            {
                var map = CreateTaxGroup(api, "bevTax", leafStore.bevTax);
                _taxMap.Add(map.LeafId, map.PosId);
            }
            if (leafStore.foodTax != 0)
            {
                var map = CreateTaxGroup(api, "foodTax", leafStore.foodTax);
                _taxMap.Add(map.LeafId, map.PosId);
            }
            if (leafStore.salesTax != 0)
            {
                var map = CreateTaxGroup(api, "salesTax", leafStore.salesTax);
                _taxMap.Add(map.LeafId, map.PosId);
            }
        }

        private static void SetupTenders(Api api, LeafDataModel.Store leafStore)
        {
            var station = api.Get(new GetConfiguration {IncludeStations = true}).Stations.First();
            var paymentConfig =
                api.Get(new GetPaymentConfigList()).Configs.First(o => o.ProcessorName.ToLower().Contains("manual"));

            foreach (var paymeth in leafStore.site_pay_methods)
            {
                CreateTender(api, paymeth, station, paymentConfig);
            }
        }

        private static Mapping CreateTaxGroup(Api api, string taxName, decimal taxValue)
        {
            var taxGroups = api.Get(new TaxGroupQuery {Limit = 100});

            var existing = taxGroups.Results.FirstOrDefault(o => o.Name == taxName);

            if (existing != null)
                return new Mapping {LeafId = taxName, PosId = existing.Id.GetValueOrDefault()};

            var taxRate = new TaxRate
            {
                AmountPerUnit = taxValue*100,
                DisplayOnReceipt = true,
                Name = taxName,
                RoundingType = RoundingMode.HalfUp,
                TaxApplicationType = TaxApplicationType.Exclusive,
                TaxType = TaxType.Percentage
            };

            taxRate = api.Post(taxRate);

            IncrementCounter(Counters.TaxRate);

            Log("Created tax rate {0}", taxRate.Name);

            var taxGroup = new TaxGroup {Name = taxName};
            taxGroup = api.Post(taxGroup);

            IncrementCounter(Counters.TaxGroup);

            var association = new TaxGroupTaxRate
            {
                TaxGroupId = taxGroup.Id.GetValueOrDefault(),
                TaxRateId = taxRate.Id.GetValueOrDefault()
            };

            api.Post(association);

            IncrementCounter(Counters.TaxGroupTaxRate);

            Log("Created tax group {0}", taxGroup.Name);

            return new Mapping {LeafId = taxName, PosId = taxGroup.Id.GetValueOrDefault()};
        }

        private static void CreateTender(Api api, LeafDataModel.SitePayMethod leafTender, Station station,
            PaymentConfiguration paymentConfig)
        {
            var tenders = api.Get(new TenderQuery {Limit = 100});

            if (tenders.Results.Any(o => o.LongDescription == leafTender.paySiteDesc.Left(30)))
                return;

            var tender = new Tender
            {
                AllowsRefunds = true,
                IsActive = true,
                IsVisible = true,
                LongDescription = leafTender.paySiteDesc.Left(30),
                NumberOfReceipts = 1,
                ShortDescription = leafTender.payTypeName.Left(4),
                TenderKind = TenderKind.General
            };

            if (tender.LongDescription.ToLower().Contains("cash"))
            {
                tender.AllowedForPayouts = true;
                tender.AllowsChange = true;
                tender.CurrencySymbol = "$";
                tender.GivenAsChange = (!tenders.Results.Any(o => o.GivenAsChange.GetValueOrDefault()));
                tender.IsPrimaryCurrency = (!tenders.Results.Any(o => o.IsPrimaryCurrency.GetValueOrDefault()));
                tender.OpensCashDrawer = true;
                tender.TenderKind = TenderKind.Cash;
            }

            if (tender.LongDescription.ToLower().Contains("debit"))
            {
                tender.AllowsCashback = true;
                tender.TenderKind = TenderKind.Debit;
            }

            if (tender.LongDescription.ToLower().Contains("credit"))
            {
                tender.RequireSignature = true;
                tender.TenderKind = TenderKind.Credit;
            }

            if (tender.LongDescription.ToLower().Contains("check"))
            {
                tender.TenderKind = TenderKind.Check;
            }

            if (tender.LongDescription.ToLower().Contains("gift"))
            {
                tender.TenderKind = TenderKind.Gift;
            }

            tender = api.Post(tender);

            IncrementCounter(Counters.Tender);

            Log("Created tender {0}", tender.LongDescription);

            var tenderProfileAssociation = new PaymentProfileTender
            {
                PaymentConfigurationId = paymentConfig.Id.GetValueOrDefault(),
                PaymentProfileId = station.PaymentProfileId.GetValueOrDefault(),
                TenderId = tender.Id.GetValueOrDefault()
            };

            api.Post(tenderProfileAssociation);

            IncrementCounter(Counters.PaymentProfileTender);

            Log("Associated tender {0} with payment profile {1}", tender.LongDescription,
                station.PaymentProfile.Name);
        }

        private static void SetupPrinters(Api api, LeafDataModel.Store leafStore)
        {
            foreach (var printer in leafStore.printers)
            {
                var map = CreatePrinter(api, printer);
                if (map != null) _printerMap.Add(map.LeafId, map.PosId);
            }
        }

        private static Mapping CreatePrinter(Api api, LeafDataModel.Printer leafPrinter)
        {
            var printers = api.Get(new GetKitchenPrinterList());

            var existing = printers.Devices.FirstOrDefault(o => o.Name == leafPrinter.printerName);

            if (existing != null)
                return new Mapping {LeafId = leafPrinter.printerName, PosId = existing.Id.GetValueOrDefault()};

            var printer = new KitchenPrinter {Name = leafPrinter.printerName};

            printer = api.Post(printer);

            IncrementCounter(Counters.KitchenPrinter);

            Log("Created printer {0}", printer.Name);

            return new Mapping {LeafId = leafPrinter.id, PosId = printer.Id.GetValueOrDefault()};
        }

        private static void SetupJobCodes(Api api, LeafDataModel.Store leafStore)
        {
            foreach (var jobCode in leafStore.job_codes)
            {
                var map = CreateJobCode(api, jobCode);
                if (map != null) _jobCodeMap.Add(map.LeafId, map.PosId);
            }
        }

        private static Mapping CreateJobCode(Api api, LeafDataModel.JobCode leafJobCode)
        {
            var jobCodes = api.Get(new JobcodesQuery {Limit = 100});

            var existing = jobCodes.Results.FirstOrDefault(o => o.Name == leafJobCode.jobCode);

            if (existing != null)
                return new Mapping {LeafId = leafJobCode.id, PosId = existing.Id.GetValueOrDefault()};

            var jobCode = new Jobcode
            {
                Name = leafJobCode.jobCode,
                AccessToPos = true,
                DefaultWage = leafJobCode.rate1,
                OvertimeMultiplier = 1.5M,
                OvertimeThreshold = leafJobCode.hrsBeforeOT,
            };

            jobCode = api.Post(jobCode);

            IncrementCounter(Counters.Jobcode);

            Log("Created jobcode {0}", jobCode.Name);

            return new Mapping {LeafId = leafJobCode.id, PosId = jobCode.Id.GetValueOrDefault()};
        }

        private static void SetupUsers(Api api, LeafDataModel.Store leafStore)
        {
            foreach (var user in leafStore.users)
            {
                CreateUser(api, user);
            }
        }

        private static void CreateUser(Api api, LeafDataModel.User leafUser)
        {
            var users = api.Get(new EmployeesQuery {Limit = 100, SearchTerm = leafUser.userName});

            var existing = users.Results.FirstOrDefault(o => o.UserFriendlyId == leafUser.id);

            if (existing != null)
                return;

            var employee = new Employee
            {
                UserFriendlyId = leafUser.id,
                DisplayName = leafUser.userName,
                FirstName = leafUser.first,
                LastName = leafUser.last
            };

            employee = api.Post(employee);

            IncrementCounter(Counters.Employee);

            Log("Created employee {0}", employee.DisplayName);

            if (!leafUser.email.IsNullOrEmpty())
            {
                var email = new EmployeeEmail
                {
                    EmailAddress = leafUser.email,
                    Label = "Personal",
                    EntityId = employee.Id
                };
                api.Post(email);

                IncrementCounter(Counters.EmployeeEmail);
            }

            if (!leafUser.phone.IsNullOrEmpty())
            {
                var phone = new EmployeePhoneNumber
                {
                    Number = leafUser.phone,
                    Label = "Personal",
                    EntityId = employee.Id
                };
                api.Post(phone);

                IncrementCounter(Counters.EmployeePhone);
            }

            foreach (var leafJobCode in leafUser.job_code_users)
            {
                var jobCode = new EmployeeJobcode
                {
                    EmployeeId = employee.Id.GetValueOrDefault(),
                    JobcodeId = _jobCodeMap[leafJobCode.job_code_id]
                };
                api.Post(jobCode);

                IncrementCounter(Counters.EmployeeJobcode);
            }
        }

        private static void SetupDepartments(Api api, LeafDataModel.Store leafStore)
        {
            foreach (var category in leafStore.catalog.categories)
            {
                var map = CreateDepartment(api, category);
                if (map != null) _departmentMap.Add(map.LeafId, map.PosId);
            }
        }

        private static Mapping CreateDepartment(Api api, LeafDataModel.Category leafCategory)
        {
            var departments = api.Get(new DepartmentsQuery {Limit = 100, SearchTerm = leafCategory.name});

            var existing = departments.Results.FirstOrDefault(o => o.UserFriendlyId == leafCategory.name);

            if (existing != null)
                return new Mapping {LeafId = leafCategory.id, PosId = existing.Id.GetValueOrDefault()};

            var department = new Department
            {
                UserFriendlyId = leafCategory.name,
                Description = leafCategory.description
            };

            if (department.Description.IsNullOrEmpty())
                department.Description = department.UserFriendlyId;

            department = api.Post(department);

            IncrementCounter(Counters.Department);

            Log("Created department {0}", department.Description);

            return new Mapping {LeafId = leafCategory.id, PosId = department.Id.GetValueOrDefault()};
        }

        private static void SetupModifiers(Api api, LeafDataModel.Store leafStore)
        {
            foreach (var modifier in leafStore.catalog.modifiers)
            {
                var map = CreateModifier(api, modifier);
                if (map != null) _modifierMap.Add(map.LeafId, map.PosId);
            }
        }

        private static Mapping CreateModifier(Api api, LeafDataModel.Modifier leafModifier)
        {
            var modifiers =
                api.Get(new ItemsQuery
                {
                    Limit = 100,
                    ModifierType = ModifierTypes.Standard,
                    SearchTerm = leafModifier.name
                });

            var existing = modifiers.Results.FirstOrDefault(o => o.Description == leafModifier.name);

            if (existing != null)
                return new Mapping {LeafId = leafModifier.id, PosId = existing.Id.GetValueOrDefault()};

            var modifier = new Item
            {
                UserFriendlyId = leafModifier.id,
                Description = leafModifier.name,
                Name = leafModifier.name,
                Price = leafModifier.price,
                Cost = leafModifier.cost,
                ModifierType = ModifierTypes.Standard
            };

            if (modifier.Description.IsNullOrEmpty())
                modifier.Description = modifier.UserFriendlyId;

            if (modifier.Name.IsNullOrEmpty())
                modifier.Name = modifier.UserFriendlyId;

            modifier = api.Post(modifier);

            IncrementCounter(Counters.Modifier);

            Log("Created modifier {0}", modifier.Name);

            return new Mapping {LeafId = leafModifier.id, PosId = modifier.Id.GetValueOrDefault()};
        }

        private static void SetupModifierGroups(Api api, LeafDataModel.Store leafStore)
        {
            foreach (var group in leafStore.catalog.modifier_groups)
            {
                var map = CreateModifierGroup(api, group);
                if (map != null) _modifierGroupsMap.Add(map.LeafId, map.PosModifierGroup);
            }
        }

        private static ModGroupMapping CreateModifierGroup(Api api, LeafDataModel.ModifierGroup leafModifierGroup)
        {
            var groups = api.Get(new ModifierGroupsQuery {Limit = 100, SearchTerm = leafModifierGroup.groupName});

            var existing = groups.Results.FirstOrDefault(o => o.Name == leafModifierGroup.groupName);

            if (existing != null)
                return new ModGroupMapping {LeafId = leafModifierGroup.id, PosModifierGroup = existing};

            int minToSelect = 0;
            int maxToSelect = 100;

            if (leafModifierGroup.modifier_group_rule != null)
            {
                minToSelect = leafModifierGroup.modifier_group_rule.minimum;
                maxToSelect = leafModifierGroup.modifier_group_rule.maximum;
            }

            var group = new ModifierGroup
            {
                MinToSelect = minToSelect,
                MaxToSelect = maxToSelect,
                Name = leafModifierGroup.groupName,
                Prompt = String.Format("Select a {0}", leafModifierGroup.groupName),
                AllowSubmodifiers = true
            };

            group = api.Post(group);

            IncrementCounter(Counters.ModifierGroup);

            Log("Created modifier group {0}", group.Name);

            foreach (var modMember in from leafModMember in leafModifierGroup.modifier_group_sub_items
                where _modifierMap.ContainsKey(leafModMember.modifier_id)
                select new ModifierGroupMember
                {
                    ItemId = _modifierMap[leafModMember.modifier_id],
                    ModifierGroupId = @group.Id.GetValueOrDefault(),
                    SortOrder = leafModMember.position,
                })
            {
                api.Post(modMember);

                IncrementCounter(Counters.ModifierGroupMember);

                Log("Created modifier group member {0}", modMember.ItemId);
            }

            return new ModGroupMapping {LeafId = leafModifierGroup.id, PosModifierGroup = group};
        }

        private static void SetupItems(Api api, LeafDataModel.Store leafStore)
        {
            var config = api.Get(new GetConfiguration {IncludeUnitsOfMeasure = true});
            foreach (var item in leafStore.catalog.items)
            {
                var map = CreateItem(api, item, config.UnitsOfMeasure);
                if (map != null) _itemMap.Add(map.LeafId, map.PosItem);
            }
        }

        private static ItemMapping CreateItem(Api api, LeafDataModel.Item leafItem,
            IEnumerable<UnitOfMeasure> unitsOfMeasure)
        {
            var items = api.Get(new ItemsQuery {Limit = 100, SearchTerm = leafItem.id});

            var existing = items.Results.FirstOrDefault(o => o.UserFriendlyId == leafItem.id);

            if (existing != null)
                return new ItemMapping {LeafId = leafItem.id, PosItem = existing};

            Guid? departmentId = GetMapValue(_departmentMap, leafItem.category_id);
            Guid? printerId = GetMapValue(_printerMap, leafItem.printer_id);
            Guid? taxGroupId = GetMapValue(_taxMap, leafItem.tax_type);
            if (!leafItem.isTaxable)
                taxGroupId = null;

            var unitOfMeasure = unitsOfMeasure.FirstOrDefault(u => u.Symbol.ToLower() == leafItem.unitType.ToLower());
            Guid? unitOfMeasureId = unitOfMeasure != null ? unitOfMeasure.Id : null;

            var item = new Item
            {
                UserFriendlyId = leafItem.id,
                Description = leafItem.name,
                Name = leafItem.name,
                Price = leafItem.price,
                Cost = leafItem.cost,
                DepartmentId = departmentId,
                TaxGroupId = taxGroupId,
                UnitOfMeasureId = unitOfMeasureId
            };

            if (item.Name.IsNullOrEmpty())
                item.Name = item.UserFriendlyId;

            item = api.Post(item);
            
            IncrementCounter(Counters.Item);

            Log("Created item {0}", item.Name);

            if (!leafItem.barcode.IsNullOrWhiteSpace())
            {
                var sku = new ItemBarcode {Barcode = leafItem.barcode, ItemId = item.Id.GetValueOrDefault()};

                sku = api.Post(sku);

                IncrementCounter(Counters.ItemBarcode);

                Log("Created barcode {0} for item {1}", sku.Barcode, item.Name);
            }

            int modGroupCounter = 0;
            foreach (var leafModGroup in leafItem.modifier_item_groups)
            {
                ModifierGroup modGroup;

                if (!_modifierGroupsMap.TryGetValue(leafModGroup.modifier_group_id, out modGroup))
                    continue;

                var itemModGroup = new ItemModifierGroup
                {
                    ItemId = item.Id.GetValueOrDefault(),
                    MaxToSelect = modGroup.MaxToSelect,
                    MinToSelect = modGroup.MinToSelect,
                    ModifierGroupId = modGroup.Id.GetValueOrDefault(),
                    Prompt = modGroup.Prompt,
                    SortOrder = modGroupCounter,
                    AllowSubmodifiers = modGroup.AllowSubmodifiers
                };
                api.Post(itemModGroup);

                IncrementCounter(Counters.ItemModifierGroup);

                Log("Associated modifier group {0} with item {1}", itemModGroup.Prompt,
                    itemModGroup.ItemId);

                modGroupCounter++;
            }

            if (printerId != null)
            {
                var printerAssociation = new KitchenPrinterItemMapping
                {
                    ItemId = item.Id.GetValueOrDefault(),
                    KitchenPrinterId = printerId.GetValueOrDefault()
                };

                api.Post(printerAssociation);

                IncrementCounter(Counters.KitchenPrinterItemMapping);

                Log("Associated printer {0} with item {1}", printerAssociation.KitchenPrinterId,
                    printerAssociation.ItemId);
            }

            return new ItemMapping {LeafId = leafItem.id, PosItem = item};
        }

        private static void SetupMenu(Api api, LeafDataModel.Store leafStore)
        {
            var tabs = api.Get(new GetMenuTabListAll());
            var mainTab = tabs.MenuTabs.First(t => t.UserFriendlyId == "Menu");

            var panels = api.Get(new GetMenuPanelListAll());

            var leftPanel = panels.MenuPanels.First(p => p.Id == mainTab.LeftPanelId);

            // Create navigation buttons for each category in the left panel
            CreateMenuCategories(api, leafStore, mainTab, leftPanel);
        }

        private static void CreateMenuCategories(Api api, LeafDataModel.Store leafStore, MenuTab mainTab, MenuPanel leftPanel)
        {
            bool isFirst = true; // We'll make the 1st panel the default

            var currentRow = 0;
            var currentPage = 1;
            foreach (var category in leafStore.catalog.categories)
            {
                if (currentRow > leftPanel.NumberOfRows - 1)
                {
                    currentPage++;
                    currentRow = 0;
                }

                // Create a panel for this category
                var panel = new MenuPanel
                {
                    UserFriendlyId = String.Format("Default.{0}", category.name),
                    MenuPanelType = MenuPanelType.Main,
                    NumberOfColumns = 4,
                    NumberOfRows = 8,
                    ContentType = MenuGrouping.Custom
                };

                panel = api.Post(panel);

                if (isFirst)
                {
                    isFirst = false;
                    mainTab.DefaultMainPanelId = panel.Id;
                    api.Put(mainTab);
                }

                IncrementCounter(Counters.MenuPanel);

                Log("Created menu panel for category {0}", category.name);

                // Create a button on the left panel to navigate to the above panel
                var button = new MenuButton
                {
                    MenuPanelId = leftPanel.Id.GetValueOrDefault(),
                    MenuButtonType = MenuButtonType.Navigation,
                    Caption = category.name,
                    ButtonFunction = panel.Id.GetValueOrDefault().ToString(),
                    Height = 1,
                    Width = 1,
                    BackColor = "#FF00B0F0",
                    XPosition = 0,
                    YPosition = currentRow,
                    Page = currentPage,
                    Hide = false,
                    Font = new Font
                    {
                        Name = "Segoe UI",
                        Size = 18,
                        Color = "#FFFFFF",
                        VerticalAlign = VerticalAlignment.Center,
                        HorizontalAlign = HorizontalAlignment.Center
                    }
                };

                api.Post(button);

                IncrementCounter(Counters.MenuButton);

                Log("Created navigation button for category {0}", category.name);

                currentRow++;

                // Create menu buttons for all of the items in this category
                CreateMenuCategoryButtons(api, leafStore, panel, category.id);
            }
        }

        private static void CreateMenuCategoryButtons(Api api, LeafDataModel.Store leafStore, MenuPanel categoryPanel,
            string leafCategoryId)
        {
            var currentX = 0;
            var currentY = 0;
            var currentPage = 1;

            foreach (var leafItem in leafStore.catalog.items.Where(i => i.category_id == leafCategoryId))
            {
                if (currentY > categoryPanel.NumberOfRows - 1)
                {
                    currentPage++;
                    currentY = 0;
                    currentX = 0;
                }

                var posItem = GetMapValue(_itemMap, leafItem.id);

                // Create a button on the left panel to navigate to the above panel
                var button = new MenuButton
                {
                    MenuPanelId = categoryPanel.Id.GetValueOrDefault(),
                    MenuButtonType = MenuButtonType.Item,
                    Caption = posItem.Name,
                    ButtonFunction = posItem.Id.ToString(),
                    Height = 1,
                    Width = 1,
                    BackColor = "#FF00B0F0",
                    XPosition = currentX,
                    YPosition = currentY,
                    Page = currentPage,
                    Hide = false,
                    Font = new Font
                    {
                        Name = "Segoe UI",
                        Size = 18,
                        Color = "#FFFFFF",
                        VerticalAlign = VerticalAlignment.Center,
                        HorizontalAlign = HorizontalAlignment.Center
                    }
                };

                api.Post(button);

                IncrementCounter(Counters.MenuButton);

                Log("Created menu button for item {0}", posItem.Name);

                currentX++;

                if (currentX > categoryPanel.NumberOfColumns - 1)
                {
                    currentY++;
                    currentX = 0;
                }
            }
        }

        private static Guid? GetMapValue(Dictionary<string, Guid> map, string key)
        {
            if (key.IsNullOrEmpty())
                return null;

            Guid tempId;
            if (map.TryGetValue(key, out tempId))
            {
                return tempId;
            }
            return null;
        }

        private static T GetMapValue<T>(Dictionary<string, T> map, string key) where T : class
        {
            if (key.IsNullOrEmpty())
                return null;

            T tempId;
            if (map.TryGetValue(key, out tempId))
            {
                return tempId;
            }
            return null;
        }
    }
}
