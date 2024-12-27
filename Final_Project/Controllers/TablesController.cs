using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Data.Entity.Core.Objects;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Final_Project.Models;

namespace Final_Project.Controllers
{
    public class TablesController : Controller
    {
        private RestaurantContext db = new RestaurantContext();

        // GET: Tables
        public ActionResult Index()
        {
            var tables = db.Tables.ToList();
            var currentTime = DateTime.Now;

            foreach (var table in tables)
            {
                // Check for any pending or confirmed future reservations
                var hasUpcomingReservation = db.Reservations
                    .Any(r => r.TableId == table.TableId &&
                             r.ReservationTime > currentTime &&
                             (r.Status == "Pending" || r.Status == "Confirmed"));

                if (hasUpcomingReservation)
                {
                    table.Status = "Reserved";
                }
                else
                {
                    var hasActiveReservation = db.Reservations
                        .Any(r => r.TableId == table.TableId &&
                                 r.Status != "Completed" &&
                                 r.Status != "Cancelled");

                    table.Status = hasActiveReservation ? "Reserved" : "Available";
                }
            }

            db.SaveChanges();
            return View(tables);
        }

        // GET: Tables/Details/5
        public ActionResult Details(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Table table = db.Tables.Find(id);
            if (table == null)
            {
                return HttpNotFound();
            }
            return View(table);
        }

        // GET: Tables/Create
        public ActionResult Create()
        {
            return View();
        }

        // POST: Tables/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details see https://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "TableId,Number,Capacity,Status")] Table table)
        {
            // Check if table number already exists
            if (db.Tables.Any(t => t.Number == table.Number))
            {
                ModelState.AddModelError("Number", "This table number already exists");
                return View(table);
            }

            if (ModelState.IsValid)
            {
                table.Status = "Available"; // Set default status
                db.Tables.Add(table);
                db.SaveChanges();
                return RedirectToAction("Index");
            }

            return View(table);
        }

        // GET: Tables/Edit/5
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Table table = db.Tables.Find(id);
            if (table == null)
            {
                return HttpNotFound();
            }
            return View(table);
        }

        // POST: Tables/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details see https://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit([Bind(Include = "TableId,Number,Capacity")] Table table)
        {
            if (ModelState.IsValid)
            {
                // Get the existing table to preserve its status
                var existingTable = await db.Tables.FindAsync(table.TableId);
                if (existingTable != null)
                {
                    // Update only number and capacity
                    existingTable.Number = table.Number;
                    existingTable.Capacity = table.Capacity;
                    // Status remains unchanged as it's managed by reservations

                    await db.SaveChangesAsync();
                    return RedirectToAction("Index");
                }
            }
            return View(table);
        }

        // GET: Tables/Delete/5
        public ActionResult Delete(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Table table = db.Tables.Find(id);
            if (table == null)
            {
                return HttpNotFound();
            }
            return View(table);
        }

        // POST: Tables/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            Table table = db.Tables.Find(id);
            db.Tables.Remove(table);
            db.SaveChanges();
            return RedirectToAction("Index");
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
