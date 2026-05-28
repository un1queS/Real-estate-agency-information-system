using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using RealEstateAgencyApp.Models;
using System.Data;

namespace RealEstateAgencyApp.UI;

public partial class MainWindow : Window
{
    /// <summary>Счётчик подавления автосохранения при программной смене ItemsSource/SelectedIndex.</summary>
    private int _suppressComboPersist;

    public MainWindow()
    {
        InitializeComponent();
        WireGridHeaderTranslations();
        Loaded += MainWindow_Loaded;
    }

    private void WireGridHeaderTranslations()
    {
        DgClients.AutoGeneratingColumn += DataGrid_OnAutoGeneratingColumn;
        DgProperties.AutoGeneratingColumn += DataGrid_OnAutoGeneratingColumn;
        DgDeals.AutoGeneratingColumn += DataGrid_OnAutoGeneratingColumn;
        DgAllTablesData.AutoGeneratingColumn += DataGrid_OnAutoGeneratingColumn;
    }

    private static readonly Dictionary<string, string> ColumnHeaderMap = new(StringComparer.OrdinalIgnoreCase)
    {
        // Clients
        ["ClientID"] = "ID клиента",
        ["FullName"] = "ФИО",
        ["Phone"] = "Телефон",
        ["Email"] = "Email",
        ["ClientType"] = "Тип клиента",

        // Properties
        ["PropertyID"] = "ID объекта",
        ["Address"] = "Адрес",
        ["PropertyType"] = "Тип объекта",
        ["Area"] = "Площадь",
        ["Rooms"] = "Комнат",
        ["Floor"] = "Этаж",
        ["Price"] = "Цена",
        ["Status"] = "Статус",
        ["OwnerID"] = "ID владельца",

        // Deals
        ["DealID"] = "ID сделки",
        ["DealType"] = "Тип сделки",
        ["Amount"] = "Сумма",
        ["DealDate"] = "Дата",
    };

    private void DataGrid_OnAutoGeneratingColumn(object? sender, DataGridAutoGeneratingColumnEventArgs e)
    {
        // Source property name for models, and column name for DataTable.
        string key = e.PropertyName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        if (ColumnHeaderMap.TryGetValue(key, out var translated))
        {
            e.Column.Header = translated;
        }
        else
        {
            // Fallback: just keep original, but strip common suffix "ID" spacing.
            e.Column.Header = key.Replace("ID", "ID", StringComparison.OrdinalIgnoreCase);
        }

        // Format dates nicer when possible.
        if (e.Column is DataGridTextColumn textCol && string.Equals(key, "DealDate", StringComparison.OrdinalIgnoreCase))
        {
            if (textCol.Binding is Binding b)
            {
                b.StringFormat = "dd.MM.yyyy";
            }
            else
            {
                textCol.Binding = new Binding(key) { StringFormat = "dd.MM.yyyy" };
            }
        }
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _suppressComboPersist++;
        try
        {
            // Устанавливаем значения по умолчанию, чтобы форма не падала при первом вводе.
            if (CbClientType.SelectedIndex < 0 && CbClientType.Items.Count > 0)
            {
                CbClientType.SelectedIndex = 0;
            }
            if (CbDealTypeComplete.SelectedIndex < 0 && CbDealTypeComplete.Items.Count > 0)
            {
                CbDealTypeComplete.SelectedIndex = 0;
            }
            if (CbDealType.SelectedIndex < 0 && CbDealType.Items.Count > 0)
            {
                CbDealType.SelectedIndex = 0;
            }
            if (CbDealStatus.SelectedIndex < 0 && CbDealStatus.Items.Count > 0)
            {
                CbDealStatus.SelectedIndex = 0;
            }
            if (CbPropertyType.SelectedIndex < 0 && CbPropertyType.Items.Count > 0)
            {
                CbPropertyType.SelectedIndex = 0;
            }
            if (CbPropertyStatus.SelectedIndex < 0 && CbPropertyStatus.Items.Count > 0)
            {
                CbPropertyStatus.SelectedIndex = 0;
            }
        }
        finally
        {
            _suppressComboPersist--;
        }

        await ReloadAllAsync();
    }

    private async Task ReloadAllAsync()
    {
        await LoadClientsAsync();
        await LoadPropertiesAsync();
        await LoadDealsAsync();
        await LoadDealSourcesAsync();
        await LoadAllTablesListAsync();
    }

    private async Task LoadClientsAsync()
    {
        if (App.Repository is null) return;
        _suppressComboPersist++;
        try
        {
            var clients = await App.Repository.GetClientsAsync();
            DgClients.ItemsSource = clients;
            CbClientsForDeal.ItemsSource = clients;
            CbDealClient.ItemsSource = clients;
            CbClientReport.ItemsSource = clients;
        }
        finally
        {
            _suppressComboPersist--;
        }
    }

    private async Task LoadPropertiesAsync()
    {
        if (App.Repository is null) return;
        _suppressComboPersist++;
        try
        {
            var properties = await App.Repository.GetPropertiesAsync();
            DgProperties.ItemsSource = properties;
            CbDealProperty.ItemsSource = properties;
        }
        finally
        {
            _suppressComboPersist--;
        }
    }

    private async Task LoadDealsAsync()
    {
        if (App.Repository is null) return;
        _suppressComboPersist++;
        try
        {
            DgDeals.ItemsSource = await App.Repository.GetDealsAsync();
        }
        finally
        {
            _suppressComboPersist--;
        }
    }

    private async Task LoadDealSourcesAsync()
    {
        if (App.Repository is null) return;
        CbActiveProperties.ItemsSource = await App.Repository.GetActivePropertiesAsync();
    }

    private async Task LoadAllTablesListAsync()
    {
        if (App.Repository is null) return;
        var tables = await App.Repository.GetAllUserTableNamesAsync();
        CbAllTables.ItemsSource = tables;
        if (tables.Count > 0)
        {
            CbAllTables.SelectedIndex = 0;
        }
        else
        {
            DgAllTablesData.ItemsSource = null;
        }
    }

    private async Task LoadSelectedTableDataAsync()
    {
        if (App.Repository is null) return;
        if (CbAllTables.SelectedItem is not string tableName || string.IsNullOrWhiteSpace(tableName))
        {
            DgAllTablesData.ItemsSource = null;
            return;
        }

        DataTable data = await App.Repository.GetTableDataAsync(tableName);
        DgAllTablesData.ItemsSource = data.DefaultView;
    }

    private async Task SafeRunAsync(Func<Task> action, string operationName)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"{operationName}: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void BtnClientsRefresh_OnClick(object sender, RoutedEventArgs e) =>
        await SafeRunAsync(LoadClientsAsync, "Ошибка загрузки клиентов");

    private async void BtnClientAdd_OnClick(object sender, RoutedEventArgs e)
    {
        await SafeRunAsync(async () =>
        {
            if (App.Repository is null) return;
            await App.Repository.AddClientAsync(new Client
            {
                FullName = TbClientName.Text.Trim(),
                Phone = TbClientPhone.Text.Trim(),
                Email = TbClientEmail.Text.Trim(),
                ClientType = ReadComboText(CbClientType, "Тип клиента")
            });
            await LoadClientsAsync();
        }, "Ошибка добавления клиента");
    }

    private async void BtnClientUpdate_OnClick(object sender, RoutedEventArgs e)
    {
        await SafeRunAsync(async () =>
        {
            if (App.Repository is null) return;
            await App.Repository.UpdateClientAsync(new Client
            {
                ClientID = ParseInt(TbClientId.Text, "ID клиента"),
                FullName = TbClientName.Text.Trim(),
                Phone = TbClientPhone.Text.Trim(),
                Email = TbClientEmail.Text.Trim(),
                ClientType = ReadComboText(CbClientType, "Тип клиента")
            });
            await LoadClientsAsync();
        }, "Ошибка обновления клиента");
    }

    private async void BtnClientDelete_OnClick(object sender, RoutedEventArgs e)
    {
        await SafeRunAsync(async () =>
        {
            if (App.Repository is null) return;
            await App.Repository.DeleteClientAsync(ParseInt(TbClientId.Text, "ID клиента"));
            await LoadClientsAsync();
        }, "Ошибка удаления клиента");
    }

    private async void BtnPropertiesRefresh_OnClick(object sender, RoutedEventArgs e) =>
        await SafeRunAsync(async () =>
        {
            await LoadPropertiesAsync();
            await LoadDealSourcesAsync();
        }, "Ошибка загрузки объектов");

    private async void BtnPropertyAdd_OnClick(object sender, RoutedEventArgs e)
    {
        await SafeRunAsync(async () =>
        {
            if (App.Repository is null) return;
            await App.Repository.AddPropertyAsync(ReadProperty());
            await LoadPropertiesAsync();
            await LoadDealSourcesAsync();
        }, "Ошибка добавления объекта");
    }

    private async void BtnPropertyUpdate_OnClick(object sender, RoutedEventArgs e)
    {
        await SafeRunAsync(async () =>
        {
            if (App.Repository is null) return;
            Property property = ReadProperty();
            property.PropertyID = ParseInt(TbPropertyId.Text, "ID объекта");
            await App.Repository.UpdatePropertyAsync(property);
            await LoadPropertiesAsync();
            await LoadDealSourcesAsync();
        }, "Ошибка обновления объекта");
    }

    private async void BtnPropertyDelete_OnClick(object sender, RoutedEventArgs e)
    {
        await SafeRunAsync(async () =>
        {
            if (App.Repository is null) return;
            await App.Repository.DeletePropertyAsync(ParseInt(TbPropertyId.Text, "ID объекта"));
            await LoadPropertiesAsync();
            await LoadDealSourcesAsync();
        }, "Ошибка удаления объекта");
    }

    private async void BtnPropertySearch_OnClick(object sender, RoutedEventArgs e)
    {
        await SafeRunAsync(async () =>
        {
            if (App.Repository is null) return;
            string type = TbSearchType.Text.Trim();
            decimal? minPrice = ParseNullableDecimal(TbSearchMinPrice.Text);
            decimal? maxPrice = ParseNullableDecimal(TbSearchMaxPrice.Text);
            int? rooms = ParseNullableInt(TbSearchRooms.Text);
            DgProperties.ItemsSource = await App.Repository.SearchPropertiesAsync(type, minPrice, maxPrice, rooms);
        }, "Ошибка поиска объектов");
    }

    private async void BtnPropertyShowActive_OnClick(object sender, RoutedEventArgs e)
    {
        await SafeRunAsync(async () =>
        {
            if (App.Repository is null) return;
            DgProperties.ItemsSource = await App.Repository.GetActivePropertiesAsync();
        }, "Ошибка загрузки активных объектов");
    }

    private async void BtnDealsRefresh_OnClick(object sender, RoutedEventArgs e) =>
        await SafeRunAsync(LoadDealsAsync, "Ошибка загрузки сделок");

    private async void BtnDealAdd_OnClick(object sender, RoutedEventArgs e)
    {
        await SafeRunAsync(async () =>
        {
            if (App.Repository is null) return;
            await App.Repository.AddDealAsync(ReadDeal());
        }, "Ошибка добавления сделки");
        await SafeRunAsync(LoadDealsAsync, "Ошибка обновления списка сделок");
    }

    private async void BtnDealUpdate_OnClick(object sender, RoutedEventArgs e)
    {
        await SafeRunAsync(async () =>
        {
            if (App.Repository is null) return;
            Deal deal = ReadDeal();
            deal.DealID = ParseInt(TbDealId.Text, "ID сделки");
            await App.Repository.UpdateDealAsync(deal);
            await LoadDealsAsync();
        }, "Ошибка обновления сделки");
    }

    private async void BtnDealDelete_OnClick(object sender, RoutedEventArgs e)
    {
        await SafeRunAsync(async () =>
        {
            if (App.Repository is null) return;
            await App.Repository.DeleteDealAsync(ParseInt(TbDealId.Text, "ID сделки"));
            await LoadDealsAsync();
        }, "Ошибка удаления сделки");
    }

    private async void BtnCompleteDeal_OnClick(object sender, RoutedEventArgs e)
    {
        await SafeRunAsync(async () =>
        {
            if (App.Repository is null) return;
            if (CbActiveProperties.SelectedItem is not Property property)
            {
                throw new InvalidOperationException("Выберите активный объект.");
            }
            if (CbClientsForDeal.SelectedItem is not Client client)
            {
                throw new InvalidOperationException("Выберите клиента.");
            }

            string dealType = ReadComboText(CbDealTypeComplete, "Тип сделки");
            decimal amount = ParseDecimal(TbDealCompleteAmount.Text, "Сумма");
            await App.Repository.CreateCompletedDealAsync(property.PropertyID, client.ClientID, dealType, amount);

            await LoadDealsAsync();
            await LoadPropertiesAsync();
            await LoadDealSourcesAsync();
            MessageBox.Show("Сделка оформлена успешно.", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
        }, "Ошибка оформления сделки");
    }

    private async void BtnReportPeriod_OnClick(object sender, RoutedEventArgs e)
    {
        await SafeRunAsync(async () =>
        {
            if (App.Repository is null) return;
            if (!DpStartDate.SelectedDate.HasValue || !DpEndDate.SelectedDate.HasValue)
            {
                throw new InvalidOperationException("Укажите обе даты периода.");
            }
            var rows = await App.Repository.GetDealsReportByPeriodAsync(
                DpStartDate.SelectedDate.Value,
                DpEndDate.SelectedDate.Value);
            LbPeriodReport.ItemsSource = rows;
        }, "Ошибка формирования отчета по периоду");
    }

    private async void BtnReportClient_OnClick(object sender, RoutedEventArgs e)
    {
        await SafeRunAsync(async () =>
        {
            if (App.Repository is null) return;
            if (CbClientReport.SelectedItem is not Client client)
            {
                throw new InvalidOperationException("Выберите клиента.");
            }
            LbClientReport.ItemsSource = await App.Repository.GetClientDealsReportAsync(client.ClientID);
        }, "Ошибка формирования отчета по клиенту");
    }

    private async void BtnRefreshAllTables_OnClick(object sender, RoutedEventArgs e)
    {
        await SafeRunAsync(LoadAllTablesListAsync, "Ошибка загрузки списка таблиц");
    }

    private async void CbAllTables_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        await SafeRunAsync(LoadSelectedTableDataAsync, "Ошибка загрузки таблицы");
    }

    private async void ComboPersistClient_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressComboPersist > 0)
        {
            return;
        }

        await SafeRunAsync(TryPersistCurrentClientAsync, "Ошибка сохранения клиента");
    }

    private async void ComboPersistProperty_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressComboPersist > 0)
        {
            return;
        }

        await SafeRunAsync(TryPersistCurrentPropertyAsync, "Ошибка сохранения объекта");
    }

    private async void ComboPersistDeal_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressComboPersist > 0)
        {
            return;
        }

        await SafeRunAsync(TryPersistCurrentDealAsync, "Ошибка сохранения сделки");
    }

    private async Task TryPersistCurrentClientAsync()
    {
        if (App.Repository is null)
        {
            return;
        }

        if (!int.TryParse(TbClientId.Text?.Trim(), out int clientId) || clientId <= 0)
        {
            return;
        }

        await App.Repository.UpdateClientAsync(new Client
        {
            ClientID = clientId,
            FullName = TbClientName.Text.Trim(),
            Phone = TbClientPhone.Text.Trim(),
            Email = TbClientEmail.Text.Trim(),
            ClientType = ReadComboText(CbClientType, "Тип клиента")
        });
        await LoadClientsAsync();
    }

    private async Task TryPersistCurrentPropertyAsync()
    {
        if (App.Repository is null)
        {
            return;
        }

        if (!int.TryParse(TbPropertyId.Text?.Trim(), out int propertyId) || propertyId <= 0)
        {
            return;
        }

        Property property = ReadProperty();
        property.PropertyID = propertyId;
        await App.Repository.UpdatePropertyAsync(property);
        await LoadPropertiesAsync();
        await LoadDealSourcesAsync();
    }

    private async Task TryPersistCurrentDealAsync()
    {
        if (App.Repository is null)
        {
            return;
        }

        if (!int.TryParse(TbDealId.Text?.Trim(), out int dealId) || dealId <= 0)
        {
            return;
        }

        Deal deal;
        try
        {
            deal = ReadDeal();
            deal.DealID = dealId;
        }
        catch (InvalidOperationException)
        {
            // Форма сделки не заполнена (дата, сумма и т.д.) — не мешаем работе со списками.
            return;
        }

        await App.Repository.UpdateDealAsync(deal);
        await LoadDealsAsync();
    }

    private Property ReadProperty()
    {
        return new Property
        {
            Address = TbPropertyAddress.Text.Trim(),
            PropertyType = ReadComboText(CbPropertyType, "Тип объекта"),
            Area = ParseDouble(TbPropertyArea.Text, "Площадь"),
            Rooms = ParseInt(TbPropertyRooms.Text, "Комнаты"),
            Floor = ParseInt(TbPropertyFloor.Text, "Этаж"),
            Price = ParseDecimal(TbPropertyPrice.Text, "Цена"),
            Status = ReadComboText(CbPropertyStatus, "Статус объекта"),
            OwnerID = ParseInt(TbPropertyOwnerId.Text, "ID владельца")
        };
    }

    private Deal ReadDeal()
    {
        if (!DpDealDate.SelectedDate.HasValue)
        {
            throw new InvalidOperationException("Укажите дату сделки.");
        }

        if (CbDealProperty.SelectedItem is not Property dealProperty)
        {
            throw new InvalidOperationException("Выберите объект в списке «Объект».");
        }

        if (CbDealClient.SelectedItem is not Client dealClient)
        {
            throw new InvalidOperationException("Выберите клиента в списке «Клиент».");
        }

        return new Deal
        {
            PropertyID = dealProperty.PropertyID,
            ClientID = dealClient.ClientID,
            DealType = ReadComboText(CbDealType, "Тип сделки"),
            Amount = ParseDecimal(TbDealAmount.Text, "Сумма"),
            DealDate = DpDealDate.SelectedDate.Value,
            Status = ReadComboText(CbDealStatus, "Статус")
        };
    }

    private static string ReadComboText(ComboBox comboBox, string fieldName)
    {
        if (comboBox.SelectedItem is ComboBoxItem item && item.Content is string text)
        {
            return text;
        }

        string typedText = comboBox.Text?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(typedText))
        {
            return typedText;
        }

        throw new InvalidOperationException($"Выберите значение в поле '{fieldName}'.");
    }

    private static int ParseInt(string value, string fieldName)
    {
        if (!int.TryParse(value, out int result))
        {
            throw new InvalidOperationException($"Поле '{fieldName}' должно быть целым числом.");
        }
        return result;
    }

    private static int? ParseNullableInt(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return int.TryParse(value, out int result) ? result : null;
    }

    private static double ParseDouble(string value, string fieldName)
    {
        if (!double.TryParse(value, out double result))
        {
            throw new InvalidOperationException($"Поле '{fieldName}' должно быть числом.");
        }
        return result;
    }

    private static decimal ParseDecimal(string value, string fieldName)
    {
        if (!decimal.TryParse(value, out decimal result))
        {
            throw new InvalidOperationException($"Поле '{fieldName}' должно быть числом.");
        }
        return result;
    }

    private static decimal? ParseNullableDecimal(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return decimal.TryParse(value, out decimal result) ? result : null;
    }
}
