using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Final_Project.Models;

namespace Final_Project.Controllers
{
    public class OrdersController : Controller
    {
        private RestaurantContext db = new RestaurantContext();

        // GET: Orders
        public ActionResult Index()
        {
            var orders = db.Orders.Include(o => o.Customer).Include(o => o.Table);
            return View(orders.ToList());
        }

        // GET: Orders/Details/5
        public ActionResult Details(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            // Properly include all related data
            var order = db.Orders
                .Include(o => o.Customer)  // Include Customer
                .Include(o => o.OrderItems.Select(oi => oi.MenuItem))  // Include MenuItem details
                .Include(o => o.Payments)  // Include Payment details
                .FirstOrDefault(o => o.OrderId == id);

            if (order == null)
            {
                return HttpNotFound();
            }

            return View(order);
        }

        // GET: Orders/Create
        public ActionResult Create()
        {
            ViewBag.CustomerId = new SelectList(db.Customers, "CustomerId", "Name");

            // Initially empty table list - will be populated via JavaScript
            ViewBag.TableId = new SelectList(new List<Table>(), "TableId", "Number");

            ViewBag.MenuItems = db.MenuItems.Select(m => new
            {
                MenuItemId = m.MenuItemId,
                Name = m.Name,
                Price = m.Price
            }).ToList();

            return View();
        }

        // POST: Orders/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details see https://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "OrderId,CustomerId,TableId,OrderTime,Status,TotalAmount")] Order order, List<int> MenuItemIds, List<int> Quantities)
        {
            if (ModelState.IsValid)
            {
                using (var transaction = db.Database.BeginTransaction())
                {
                    try
                    {
                        order.OrderTime = DateTime.Now;
                        order.Status = "Pending";
                        decimal totalAmount = 0;

                        // First save the order
                        db.Orders.Add(order);
                        db.SaveChanges();

                        // Create order items
                        for (int i = 0; i < MenuItemIds.Count; i++)
                        {
                            var menuItem = db.MenuItems.Find(MenuItemIds[i]);
                            if (menuItem != null)
                            {
                                var orderItem = new OrderItem
                                {
                                    OrderId = order.OrderId,
                                    MenuItemId = menuItem.MenuItemId,
                                    Quantity = Quantities[i],
                                    Price = menuItem.Price
                                };

                                totalAmount += menuItem.Price * Quantities[i];
                                db.OrderItems.Add(orderItem);
                            }
                        }

                        // Update order total and table status
                        order.TotalAmount = totalAmount;

                        if (order.TableId.HasValue)
                        {
                            var table = db.Tables.Find(order.TableId);
                            if (table != null)
                            {
                                table.Status = "Occupied";
                                db.Entry(table).State = EntityState.Modified;
                            }
                        }

                        db.SaveChanges();
                        transaction.Commit();
                        TempData["SuccessMessage"] = "Order created successfully.";
                        return RedirectToAction("Index");
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        ModelState.AddModelError("", "Error creating order: " + ex.Message);
                    }
                }
            }

            ViewBag.CustomerId = new SelectList(db.Customers, "CustomerId", "Name", order.CustomerId);
            ViewBag.TableId = new SelectList(db.Tables.Where(t => t.Status == "Available"), "TableId", "Number", order.TableId);
            ViewBag.MenuItems = db.MenuItems.Select(m => new { m.MenuItemId, m.Name, m.Price }).ToList();
            return View(order);
        }

        [HttpGet]
        public JsonResult GetCustomerTable(int customerId)
        {
            try
            {
                // For debugging - log the received customerId
                System.Diagnostics.Debug.WriteLine($"Received CustomerId: {customerId}");

                var reservation = db.Reservations
                    .Where(r => r.CustomerId == customerId &&
                               r.Status == "Confirmed" &&
                               r.ReservationTime.Date == DateTime.Today)
                    .Include(r => r.Table)
                    .FirstOrDefault();

                // Log what we found
                System.Diagnostics.Debug.WriteLine($"Found reservation: {reservation != null}");

                if (reservation != null && reservation.Table != null)
                {
                    return Json(new
                    {
                        success = true,
                        hasReservation = true,
                        tableId = reservation.TableId,
                        tableNumber = reservation.Table.Number,
                        debug = $"Found table {reservation.Table.Number} for customer {customerId}"
                    }, JsonRequestBehavior.AllowGet);
                }

                return Json(new
                {
                    success = true,
                    hasReservation = false,
                    debug = $"No reservation found for customer {customerId}"
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // GET: Orders/Edit/5
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Order order = db.Orders.Find(id);
            if (order == null)
            {
                return HttpNotFound();
            }
            ViewBag.CustomerId = new SelectList(db.Customers, "CustomerId", "Name", order.CustomerId);
            ViewBag.TableId = new SelectList(db.Tables, "TableId", "Number", order.TableId);
            return View(order);
        }

        // POST: Orders/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details see https://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(Order order)
        {
            if (ModelState.IsValid)
            {
                using (var transaction = db.Database.BeginTransaction())
                {
                    try
                    {
                        var originalOrder = db.Orders.Include(o => o.Table)
                            .FirstOrDefault(o => o.OrderId == order.OrderId);

                        if (originalOrder != null)
                        {
                            // Update table statuses if table has changed
                            if (originalOrder.TableId != order.TableId)
                            {
                                // Free up the old table
                                if (originalOrder.TableId.HasValue)
                                {
                                    var oldTable = db.Tables.Find(originalOrder.TableId);
                                    if (oldTable != null)
                                    {
                                        oldTable.Status = "Available";
                                        db.Entry(oldTable).State = EntityState.Modified;
                                    }
                                }

                                // Mark new table as occupied
                                if (order.TableId.HasValue)
                                {
                                    var newTable = db.Tables.Find(order.TableId);
                                    if (newTable != null)
                                    {
                                        newTable.Status = "Occupied";
                                        db.Entry(newTable).State = EntityState.Modified;
                                    }
                                }
                            }

                            db.Entry(order).State = EntityState.Modified;
                            db.SaveChanges();
                            transaction.Commit();

                            TempData["SuccessMessage"] = "Order updated successfully!";
                            return RedirectToAction("Index");
                        }
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        ModelState.AddModelError("", "Error updating order: " + ex.Message);
                    }
                }
            }

            ViewBag.CustomerId = new SelectList(db.Customers, "CustomerId", "Name", order.CustomerId);
            ViewBag.TableId = new SelectList(db.Tables, "TableId", "Number", order.TableId);
            return View(order);
        }

        // GET: Orders/Delete/5
        public ActionResult Delete(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Order order = db.Orders
                .Include(o => o.Customer)
                .Include(o => o.OrderItems)
                .FirstOrDefault(o => o.OrderId == id);

            if (order == null)
            {
                return HttpNotFound();
            }
            return View(order);
        }

        // POST: Orders/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteConfirmed(int id)
        {
            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    var order = await db.Orders
                        .Include(o => o.OrderItems)
                        .Include(o => o.Payments)
                        .FirstOrDefaultAsync(o => o.OrderId == id);

                    if (order == null)
                    {
                        return HttpNotFound();
                    }

                    // First delete related OrderItems
                    foreach (var item in order.OrderItems.ToList())
                    {
                        db.OrderItems.Remove(item);
                    }

                    // Then delete related Payments
                    foreach (var payment in order.Payments.ToList())
                    {
                        db.Payments.Remove(payment);
                    }

                    // Finally delete the order
                    db.Orders.Remove(order);
                    await db.SaveChangesAsync();
                    transaction.Commit();

                    TempData["SuccessMessage"] = "Order deleted successfully.";
                    return RedirectToAction("Index");
                }
                catch (Exception)
                {
                    transaction.Rollback();
                    TempData["ErrorMessage"] = "An error occurred while deleting the order.";
                    return RedirectToAction("Index");
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
