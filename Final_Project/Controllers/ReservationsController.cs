using System;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Mvc;
using Final_Project.Models;

namespace Final_Project.Controllers
{
    public class ReservationsController : Controller
    {
        private RestaurantContext db = new RestaurantContext();

        // GET: Reservations
        public async Task<ActionResult> Index()
        {
            var reservations = await db.Reservations
                .Include(r => r.Customer)
                .Include(r => r.Table)
                .OrderByDescending(r => r.ReservationTime)
                .ToListAsync();
            return View(reservations);
        }

        // GET: Reservations/Details/5
        public async Task<ActionResult> Details(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            var reservation = await db.Reservations
                .Include(r => r.Customer)
                .Include(r => r.Table)
                .FirstOrDefaultAsync(r => r.ReservationId == id);

            if (reservation == null)
            {
                return HttpNotFound();
            }
            return View(reservation);
        }

        // GET: Reservations/Create
        // GET: Reservations/Create
        public ActionResult Create()
        {
            // Get available tables and convert to a list first
            var availableTables = db.Tables
                .Where(t => t.Status == "Available")
                .ToList()  // Bring data into memory first
                .Select(t => new
                {
                    TableId = t.TableId,
                    DisplayText = string.Format("Table {0} ({1} Seats)", t.Number, t.Capacity)
                });

            ViewBag.TableId = new SelectList(availableTables, "TableId", "DisplayText");
            return View();
        }

        // POST: Reservations/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create(string CustomerName, string CustomerPhone, string CustomerEmail, Reservation reservation)
        {
            if (ModelState.IsValid)
            {
                using (var transaction = db.Database.BeginTransaction())
                {
                    try
                    {
                        // Create or update customer
                        var customer = await db.Customers
                            .FirstOrDefaultAsync(c => c.Name == CustomerName && c.Phone == CustomerPhone);

                        if (customer == null)
                        {
                            customer = new Customer
                            {
                                Name = CustomerName,
                                Phone = CustomerPhone,
                                Email = CustomerEmail
                            };
                            db.Customers.Add(customer);
                            await db.SaveChangesAsync();
                        }

                        // Create reservation
                        reservation.CustomerId = customer.CustomerId;
                        db.Reservations.Add(reservation);

                        // Update table status
                        var table = await db.Tables.FindAsync(reservation.TableId);
                        if (table != null)
                        {
                            table.Status = "Reserved";
                        }

                        await db.SaveChangesAsync();
                        transaction.Commit();
                        return RedirectToAction("Index");
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
            ViewBag.CustomerId = new SelectList(db.Customers, "CustomerId", "Name", reservation.CustomerId);
            ViewBag.TableId = new SelectList(
                db.Tables.Select(t => new
                {
                    t.TableId,
                    DisplayName = $"Table {t.Number} ({t.Capacity} Seats)"
                }),
                "TableId",
                "DisplayName",
                reservation.TableId);
            return View(reservation);
        }

        // GET: Reservations/Edit/5
        public async Task<ActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            var reservation = await db.Reservations.FindAsync(id);
            if (reservation == null)
            {
                return HttpNotFound();
            }

            ViewBag.CustomerId = new SelectList(db.Customers, "CustomerId", "Name", reservation.CustomerId);
            ViewBag.TableId = new SelectList(db.Tables, "TableId", "Number", reservation.TableId);
            return View(reservation);
        }

        // POST: Reservations/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit([Bind(Include = "ReservationId,CustomerId,TableId,ReservationTime,Status")] Reservation reservation)
        {
            if (ModelState.IsValid)
            {
                using (var transaction = db.Database.BeginTransaction())
                {
                    try
                    {
                        var originalReservation = await db.Reservations
                            .AsNoTracking()
                            .FirstOrDefaultAsync(r => r.ReservationId == reservation.ReservationId);

                        db.Entry(reservation).State = EntityState.Modified;

                        // Handle table status changes based on reservation status
                        if (originalReservation != null && originalReservation.Status != reservation.Status)
                        {
                            var table = await db.Tables.FindAsync(reservation.TableId);
                            if (table != null)
                            {
                                if (reservation.Status == "Completed" || reservation.Status == "Cancelled")
                                {
                                    // Check if there are no other active reservations for this table
                                    var hasOtherActiveReservations = await db.Reservations
                                        .AnyAsync(r => r.TableId == table.TableId &&
                                                r.ReservationId != reservation.ReservationId &&
                                                (r.Status == "Confirmed" || r.Status == "Pending"));

                                    if (!hasOtherActiveReservations)
                                    {
                                        table.Status = "Available";
                                    }
                                }
                                else if (reservation.Status == "Confirmed")
                                {
                                    table.Status = "Reserved";
                                }
                            }
                        }

                        await db.SaveChangesAsync();
                        transaction.Commit();
                        return RedirectToAction("Index");
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }

            ViewBag.CustomerId = new SelectList(db.Customers, "CustomerId", "Name", reservation.CustomerId);
            ViewBag.TableId = new SelectList(db.Tables, "TableId", "Number", reservation.TableId);
            return View(reservation);
        }

        // GET: Reservations/Delete/5
        public async Task<ActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            var reservation = await db.Reservations
                .Include(r => r.Customer)
                .Include(r => r.Table)
                .FirstOrDefaultAsync(r => r.ReservationId == id);

            if (reservation == null)
            {
                return HttpNotFound();
            }
            return View(reservation);
        }

        // POST: Reservations/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteConfirmed(int id)
        {
            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    var reservation = await db.Reservations.FindAsync(id);
                    if (reservation != null)
                    {
                        // Update table status if this was the only active reservation
                        var table = await db.Tables.FindAsync(reservation.TableId);
                        if (table != null)
                        {
                            var hasOtherActiveReservations = await db.Reservations
                                .AnyAsync(r => r.TableId == table.TableId &&
                                        r.ReservationId != id &&
                                        (r.Status == "Confirmed" || r.Status == "Pending"));

                            if (!hasOtherActiveReservations)
                            {
                                table.Status = "Available";
                            }
                        }

                        db.Reservations.Remove(reservation);
                        await db.SaveChangesAsync();
                        transaction.Commit();
                    }
                    return RedirectToAction("Index");
                }
                catch
                {
                    transaction.Rollback();
                    throw;
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