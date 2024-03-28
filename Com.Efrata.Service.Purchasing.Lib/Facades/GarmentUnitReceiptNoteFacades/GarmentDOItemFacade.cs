using AutoMapper;
using Com.Efrata.Service.Purchasing.Lib.Helpers;
using Com.Efrata.Service.Purchasing.Lib.Helpers.ReadResponse;
using Com.Efrata.Service.Purchasing.Lib.Interfaces;
using Com.Efrata.Service.Purchasing.Lib.Models.GarmentDeliveryOrderModel;
using Com.Efrata.Service.Purchasing.Lib.Models.GarmentExternalPurchaseOrderModel;
using Com.Efrata.Service.Purchasing.Lib.Models.GarmentInternalPurchaseOrderModel;
using Com.Efrata.Service.Purchasing.Lib.Models.GarmentInventoryModel;
using Com.Efrata.Service.Purchasing.Lib.Models.GarmentPurchaseRequestModel;
using Com.Efrata.Service.Purchasing.Lib.Models.GarmentUnitDeliveryOrderModel;
using Com.Efrata.Service.Purchasing.Lib.Models.GarmentUnitExpenditureNoteModel;
using Com.Efrata.Service.Purchasing.Lib.Models.GarmentUnitReceiptNoteModel;
using Com.Efrata.Service.Purchasing.Lib.Services;
using Com.Efrata.Service.Purchasing.Lib.ViewModels.GarmentUnitReceiptNoteViewModels;
using Com.Moonlay.Models;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading.Tasks;

namespace Com.Efrata.Service.Purchasing.Lib.Facades.GarmentUnitReceiptNoteFacades
{
    public class GarmentDOItemFacade : IGarmentDOItemFacade
    {
        private readonly string USER_AGENT = "Facade";

        private readonly PurchasingDbContext dbContext;
        private readonly IServiceProvider serviceProvider;
        private readonly IdentityService identityService;

        private readonly DbSet<GarmentDOItems> dbSetGarmentDOItems;
        private readonly DbSet<GarmentUnitReceiptNote> dbSetGarmentUnitReceiptNote;
        private readonly DbSet<GarmentUnitReceiptNoteItem> dbSetGarmentUnitReceiptNoteItem;
        private readonly DbSet<GarmentExternalPurchaseOrderItem> dbSetGarmentExternalPurchaseOrderItem;
        private readonly DbSet<GarmentPurchaseRequestItem> dbSetGarmentPRrItem;

        public GarmentDOItemFacade(IServiceProvider serviceProvider, PurchasingDbContext dbContext)
        {
            this.serviceProvider = serviceProvider;
            identityService = (IdentityService)serviceProvider.GetService(typeof(IdentityService));

            dbSetGarmentDOItems = dbContext.Set<GarmentDOItems>();
            dbSetGarmentUnitReceiptNote = dbContext.Set<GarmentUnitReceiptNote>();
            dbSetGarmentUnitReceiptNoteItem = dbContext.Set<GarmentUnitReceiptNoteItem>();
            dbSetGarmentExternalPurchaseOrderItem = dbContext.Set<GarmentExternalPurchaseOrderItem>();
            dbSetGarmentPRrItem = dbContext.Set<GarmentPurchaseRequestItem>();
        }

        public List<object> ReadForUnitDO(string Keyword = null, string Filter = "{}")
        {
            var GarmentDOItemsQuery = dbSetGarmentDOItems.Where(entity => entity.RemainingQuantity > 0).Select(a => new { a.Id, a.EPOItemId, a.URNItemId, a.UnitId, a.StorageId, a.RO, a.POSerialNumber });
            IQueryable<GarmentUnitReceiptNoteItem> GarmentUnitReceiptNoteItemsQuery = dbSetGarmentUnitReceiptNoteItem;
            //IQueryable<GarmentUnitReceiptNote> GarmentUnitReceiptNotesQuery = dbSetGarmentUnitReceiptNote;
            //IQueryable<GarmentExternalPurchaseOrderItem> GarmentExternalPurchaseOrderItemsQuery = dbSetGarmentExternalPurchaseOrderItem;

            Dictionary<string, string> FilterDictionary = JsonConvert.DeserializeObject<Dictionary<string, string>>(Filter);
            long unitId = 0;
            long storageId = 0;
            long doItemsId = 0;
            bool hasUnitFilter = FilterDictionary.ContainsKey("UnitId") && long.TryParse(FilterDictionary["UnitId"], out unitId);
            bool hasStorageFilter = FilterDictionary.ContainsKey("StorageId") && long.TryParse(FilterDictionary["StorageId"], out storageId);
            bool hasRONoFilter = FilterDictionary.ContainsKey("RONo");
            bool hasPOSerialNumberFilter = FilterDictionary.ContainsKey("POSerialNumber");
            string RONo = hasRONoFilter ? (FilterDictionary["RONo"] ?? "").Trim() : "";
            string POSerialNumber = hasPOSerialNumberFilter ? (FilterDictionary["POSerialNumber"] ?? "").Trim() : "";
            bool hasDOItemIdFilter = FilterDictionary.ContainsKey("DOItemsId") && long.TryParse(FilterDictionary["DOItemsId"], out doItemsId);

            if (hasDOItemIdFilter)
            {
                GarmentDOItemsQuery = GarmentDOItemsQuery.Where(x => x.Id == doItemsId);
            }
            else
            {
                if (hasUnitFilter)
                {
                    GarmentDOItemsQuery = GarmentDOItemsQuery.Where(x => x.UnitId == unitId);
                }
                if (hasStorageFilter)
                {
                    GarmentDOItemsQuery = GarmentDOItemsQuery.Where(x => x.StorageId == storageId);
                }
                if (hasRONoFilter)
                {
                    GarmentDOItemsQuery = GarmentDOItemsQuery.Where(x => x.RO == RONo);
                }
                if (hasPOSerialNumberFilter)
                {
                    GarmentDOItemsQuery = GarmentDOItemsQuery.Where(x => x.POSerialNumber == POSerialNumber);
                }
            }


            List<object> ListData = new List<object>();
            var data = from doi in GarmentDOItemsQuery
                       join urni in GarmentUnitReceiptNoteItemsQuery.IgnoreQueryFilters() on doi.URNItemId equals urni.Id
                       where (urni.IsDeleted == true && urni.DeletedAgent == "LUCIA") || (urni.IsDeleted == false)
                       select new
                       {
                           DOItemsId = doi.Id,
                           urni.URNId,
                           doi.URNItemId,
                           doi.EPOItemId,
                       };
            var urnIds = data.Select(s => s.URNId).ToList().Distinct().ToList();
            var URNs = dbSetGarmentUnitReceiptNote.IgnoreQueryFilters().Where(u => urnIds.Contains(u.Id))
                .Select(s => new { s.Id, s.URNNo }).ToList();
            var urnItemIds = data.Select(s => s.URNItemId).ToList().Distinct().ToList();
            var urnItems = dbSetGarmentUnitReceiptNoteItem.IgnoreQueryFilters().Where(w => urnItemIds.Contains(w.Id))
                .Select(s => new { s.Id, s.DODetailId, s.ProductRemark, s.PricePerDealUnit, s.ReceiptCorrection, s.CorrectionConversion }).ToList();

            var epoItemIds = data.Select(s => s.EPOItemId).ToList().Distinct().ToList();
            var epoItems = dbSetGarmentExternalPurchaseOrderItem.IgnoreQueryFilters().Where(w => epoItemIds.Contains(w.Id))
                .Select(s => new { s.Id, s.Article }).ToList().ToList();

            var DOItemIds = data.Select(s => s.DOItemsId).Distinct().ToList();
            var DOItems = dbSetGarmentDOItems.Where(w => DOItemIds.Contains(w.Id))
                .Select(s => new
                {
                    DOItemsId = s.Id,
                    s.POItemId,
                    s.URNItemId,
                    s.EPOItemId,
                    s.PRItemId,
                    s.ProductId,
                    s.ProductCode,
                    s.ProductName,
                    s.SmallQuantity,
                    s.SmallUomId,
                    s.SmallUomUnit,
                    s.DesignColor,
                    s.POSerialNumber,
                    s.RemainingQuantity,
                    s.CustomsCategory,
                    RONo = s.RO
                }).ToList();
            foreach (var item in data)
            {
                var urn = URNs.FirstOrDefault(f => f.Id.Equals(item.URNId));
                var urnItem = urnItems.FirstOrDefault(f => f.Id.Equals(item.URNItemId));
                var epoItem = epoItems.FirstOrDefault(f => f.Id.Equals(item.EPOItemId));
                var doItem = DOItems.FirstOrDefault(f => f.DOItemsId.Equals(item.DOItemsId));

                ListData.Add(new
                {
                    doItem.DOItemsId,
                    URNId = urn.Id,
                    urn.URNNo,
                    doItem.POItemId,
                    doItem.URNItemId,
                    doItem.EPOItemId,
                    doItem.PRItemId,
                    doItem.ProductId,
                    doItem.ProductCode,
                    doItem.ProductName,
                    doItem.SmallQuantity,
                    doItem.SmallUomId,
                    doItem.SmallUomUnit,
                    doItem.DesignColor,
                    doItem.POSerialNumber,
                    doItem.RemainingQuantity,
                    doItem.RONo,
                    epoItem.Article,
                    urnItem.DODetailId,
                    urnItem.ProductRemark,
                    urnItem.PricePerDealUnit,
                    urnItem.ReceiptCorrection,
                    doItem.CustomsCategory,
                    urnItem.CorrectionConversion
                });
            }

            return ListData;
        }

        public List<object> ReadForUnitDOMore(string Keyword = null, string Filter = "{}", int size = 50)
        {
            IQueryable<GarmentDOItems> GarmentDOItemsQuery = dbSetGarmentDOItems.Where(w => w.IsDeleted == false);

            Dictionary<string, string> FilterDictionary = JsonConvert.DeserializeObject<Dictionary<string, string>>(Filter);
            long unitId = 0;
            long storageId = 0;
            bool hasUnitFilter = FilterDictionary.ContainsKey("UnitId") && long.TryParse(FilterDictionary["UnitId"], out unitId);
            bool hasStorageFilter = FilterDictionary.ContainsKey("StorageId") && long.TryParse(FilterDictionary["StorageId"], out storageId);
            bool hasRONoFilter = FilterDictionary.ContainsKey("RONo");
            string RONo = hasRONoFilter ? (FilterDictionary["RONo"] ?? "").Trim() : "";

            if (hasUnitFilter)
            {
                GarmentDOItemsQuery = GarmentDOItemsQuery.Where(x => x.UnitId == unitId);
            }
            if (hasStorageFilter)
            {
                GarmentDOItemsQuery = GarmentDOItemsQuery.Where(x => x.StorageId == storageId);
            }
            if (hasRONoFilter)
            {
                GarmentDOItemsQuery = GarmentDOItemsQuery.Where(x => x.RO != RONo);
            }

            Keyword = (Keyword ?? "").Trim();
            GarmentDOItemsQuery = GarmentDOItemsQuery.Where(x => x.RemainingQuantity > 0 && (x.RO.Contains(Keyword) || x.POSerialNumber.Contains(Keyword) || x.ProductName.Contains(Keyword) || x.ProductCode.Contains(Keyword)));

            var data = from doi in GarmentDOItemsQuery
                       where doi.RemainingQuantity > 0
                       && (doi.RO.Contains(Keyword) || doi.POSerialNumber.Contains(Keyword) || doi.ProductName.Contains(Keyword) || doi.ProductCode.Contains(Keyword))
                       select new
                       {
                           doi.URNItemId,
                           RONo = doi.RO,
                           doi.ProductName,
                           doi.ProductCode,
                           doi.POSerialNumber,
                           doi.RemainingQuantity,
                           DOItemsId = doi.Id
                       };

            List<object> ListData = new List<object>(data.OrderBy(o => o.RONo).Take(size));
            return ListData;
        }
        //

        public ReadResponse<dynamic> ReadForCC(int Page = 1, int Size = 25, string Order = "{}", string Keyword = null, string Filter = "{}", string Select = null, string Search = "[]")
        {
            IQueryable<GarmentDOItems> Query = dbSetGarmentDOItems.Where(x => x.RemainingQuantity > 0);

            List<string> SearchAttributes = JsonConvert.DeserializeObject<List<string>>(Search);
            if (SearchAttributes.Count < 1)
            {
                SearchAttributes = new List<string>() { "POSerialNumber", "RO" };
            }

            IQueryable<GarmentPurchaseRequestItem> QueryPRItem = dbSetGarmentPRrItem;

            //Query = QueryHelper<GarmentDOItems>.ConfigureSearch(Query, SearchAttributes, Keyword, WithAny: false);

            //Dictionary<string, string> FilterDictionary = JsonConvert.DeserializeObject<Dictionary<string, string>>(Filter);
            //Query = QueryHelper<GarmentDOItems>.ConfigureFilter(Query, FilterDictionary);

            //Dictionary<string, string> OrderDictionary = JsonConvert.DeserializeObject<Dictionary<string, string>>(Order);
            //Query = QueryHelper<GarmentDOItems>.ConfigureOrder(Query, OrderDictionary);

            var QueryDOItem = (from a in Query
                               join b in QueryPRItem on a.PRItemId equals b.Id
                               where (string.IsNullOrWhiteSpace(Keyword) ? true : (a.POSerialNumber.Contains(Keyword) || a.RO.Contains(Keyword)))

                               select new GarmentDOItemForCCViewModel
                               {
                                   GarmentPRId = a.Id,
                                   GarmentPRItemId = a.PRItemId,
                                   POSerialNumber = a.POSerialNumber,
                                   DOItemNo = a.DOItemNo,
                                   RONo = a.RO,
                                   CategoryId = b.CategoryId,
                                   CategoryCode = b.CategoryName == "FABRIC" ? "FAB" : b.ProductCode.Substring(0, 3),
                                   CategoryName = b.CategoryName,
                                   ProductId = a.ProductId,
                                   ProductCode = a.ProductCode,
                                   ProductName = a.ProductName,
                                   ProductRemark = b.ProductRemark,
                                   UOMId = a.SmallUomId,
                                   UOMUnit = a.SmallUomUnit,
                                   BudgetPrice = b.BudgetPrice,
                                   RemainingQuantity = a.RemainingQuantity
                               });

            IQueryable SelectedQuery = QueryDOItem;
            if (!string.IsNullOrWhiteSpace(Select))
            {
                SelectedQuery = QueryDOItem.Select(Select);
            }

            int totalData = SelectedQuery.Count();

            if (Size > 0)
            {
                SelectedQuery = SelectedQuery
                    .Skip((Page - 1) * Size)
                    .Take(Size);
            }

            List<dynamic> Data = SelectedQuery
                .ToDynamicList();

            return new ReadResponse<dynamic>(Data, totalData, null);
        }

        public async Task<int> Patch(string id, JsonPatchDocument<GarmentDOItems> jsonPatch)
        {
            int Updated = 0;

            using (var transaction = dbContext.Database.BeginTransaction())
            {
                try
                {
                    var IDs = JsonConvert.DeserializeObject<List<long>>(id);
                    foreach (var ID in IDs)
                    {
                        var data = dbSetGarmentDOItems.Where(d => d.Id == ID)
                            .Single();

                        EntityExtension.FlagForUpdate(data, identityService.Username, USER_AGENT);

                        jsonPatch.ApplyTo(data);
                    }

                    Updated = await dbContext.SaveChangesAsync();
                    transaction.Commit();
                }
                catch (Exception e)
                {
                    transaction.Rollback();
                    throw e;
                }
            }

            return Updated;
        }
    }
}
