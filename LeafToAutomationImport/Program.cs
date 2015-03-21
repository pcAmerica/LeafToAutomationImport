using System;
using System.Collections.Generic;
using System.Linq;
using dotNetExt;
using pcAmerica.Desktop.POS.ServiceModel.Operations;
using pcAmerica.Desktop.POS.ServiceModel.Operations.Menus;
using pcAmerica.Desktop.POS.ServiceModel.Types;
using pcAmerica.Desktop.POS.ServiceModel.Types.Payments;

namespace LeafToAutomationImport
{
    internal class Program
    {
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

        private static Dictionary<string, Guid> _taxMap;
        private static Dictionary<string, Guid> _printerMap;
        private static Dictionary<string, Guid> _jobCodeMap;
        private static Dictionary<string, Guid> _departmentMap;
        private static Dictionary<string, Guid> _modifierMap;
        private static Dictionary<string, ModifierGroup> _modifierGroupsMap;
        private static Dictionary<string, Item> _itemMap;
        private static Dictionary<string, Guid> _categoryPanelMap;

        private static void Main(string[] args)
        {
            _taxMap = new Dictionary<string, Guid>();
            _printerMap = new Dictionary<string, Guid>();
            _jobCodeMap = new Dictionary<string, Guid>();
            _departmentMap = new Dictionary<string, Guid>();
            _modifierMap = new Dictionary<string, Guid>();
            _modifierGroupsMap = new Dictionary<string, ModifierGroup>();
            _itemMap = new Dictionary<string, Item>();
            _categoryPanelMap = new Dictionary<string, Guid>();

            using (var reader = System.IO.File.OpenText("site_export_sample.json"))
            {
                var leafStore = ServiceStack.Text.JsonSerializer.DeserializeFromReader<LeafDataModel.Store>(reader);

                var api = new Api {Apikey = "as46QsUS8RjTchZg", BaseUri = "http://localhost:52454"};

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
        }

        private static void SetupStore(Api api, LeafDataModel.Store leafStore)
        {
            var posStore = api.Get(new Store());
            posStore.Name = leafStore.siteName;
            posStore.Email = leafStore.siteEmail;
            posStore.PhoneNumber = leafStore.sitePhone;
            posStore.Address = new StoreAddress
            {
                City = leafStore.primary_address.city,
                PostalCode = leafStore.primary_address.postalCode,
                State = leafStore.primary_address.stateShort,
                Street = leafStore.primary_address.address,
                Street2 = leafStore.primary_address.address2,
                Country = ""
            };

            api.Put(posStore);
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
            foreach (var paymeth in leafStore.site_pay_methods)
            {
                CreateTender(api, paymeth);
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

            var taxGroup = new TaxGroup {Name = taxName};
            taxGroup = api.Post(taxGroup);

            var association = new TaxGroupTaxRate
            {
                TaxGroupId = taxGroup.Id.GetValueOrDefault(),
                TaxRateId = taxRate.Id.GetValueOrDefault()
            };

            api.Post(association);

            return new Mapping {LeafId = taxName, PosId = taxGroup.Id.GetValueOrDefault()};
        }

        private static void CreateTender(Api api, LeafDataModel.SitePayMethod leafTender)
        {
            var tenders = api.Get(new TenderQuery {Limit = 100});

            if (tenders.Results.Any(o => o.LongDescription == leafTender.paySiteDesc.Left(30)))
                return;

            var tender = new Tender
            {
                AllowsRefunds = true,
                IsActive = true,
                IsVisible = true,
                LongDescription = leafTender.paySiteDesc,
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

            api.Post(tender);
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

            if (!leafUser.email.IsNullOrEmpty())
            {
                var email = new EmployeeEmail
                {
                    EmailAddress = leafUser.email,
                    Label = "Personal",
                    EntityId = employee.Id
                };
                api.Post(email);
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
            }

            foreach (var leafJobCode in leafUser.job_code_users)
            {
                var jobCode = new EmployeeJobcode
                {
                    EmployeeId = employee.Id.GetValueOrDefault(),
                    JobcodeId = _jobCodeMap[leafJobCode.job_code_id]
                };
                api.Post(jobCode);
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
                Prompt = String.Format("Select a {0}", leafModifierGroup.groupName)
            };

            group = api.Post(group);

            foreach (var modMember in from leafModMember in leafModifierGroup.modifier_group_sub_items
                where _modifierMap.ContainsKey(leafModMember.modifier_id)
                select new ModifierGroupMember
                {
                    ItemId = _modifierMap[leafModMember.modifier_id],
                    ModifierGroupId = @group.Id.GetValueOrDefault(),
                    SortOrder = leafModMember.position
                })
            {
                api.Post(modMember);
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
                    SortOrder = modGroupCounter
                };
                api.Post(itemModGroup);

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
            }

            return new ItemMapping {LeafId = leafItem.id, PosItem = item};
        }

        private static void SetupMenu(Api api, LeafDataModel.Store leafStore)
        {
            var panels = api.Get(new GetMenuPanelListAll());

            var leftPanel = panels.MenuPanels.First(p => p.MenuPanelType == MenuPanelType.Left);
            var categoryButtons = new List<MenuButton>();

            // Create navigation buttons for each category in the left panel
            var currentRow = 0;
            var currentPage = 1;
            foreach (var category in leafStore.catalog.categories)
            {
                if (currentRow > leftPanel.NumberOfRows)
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

                _categoryPanelMap.Add(category.id, panel.Id.GetValueOrDefault());

                // Create a button on the left panel to navigate to the above panel
                var button = new MenuButton
                {
                    MenuPanelId = panel.Id.GetValueOrDefault(),
                    MenuButtonType = MenuButtonType.Navigation,
                    Caption = category.name,
                    Height = 1,
                    Width = 1,
                    BackColor = "#FF00B0F0",
                    XPosition = 0,
                    YPosition = currentPage,
                    Page = currentPage,
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
                currentRow++;
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
    }
}
