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
    public class CustomersController : Controller
    {
        private RestaurantContext db = new RestaurantContext();

        // GET: Customers
        public ActionResult Index()
        {
            return View(db.Customers.ToList());
        }

        // GET: Customers/Details/5
        public ActionResult Details(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Customer customer = db.Customers.Find(id);
            if (customer == null)
            {
                return HttpNotFound();
            }
            return View(customer);
        }

        // GET: Customers/Create
        public ActionResult Create()
        {
            return View();
        }

        // POST: Customers/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details see https://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "CustomerId,Name,Email,Phone")] Customer customer)
        {
            if (ModelState.IsValid)
            {
                db.Customers.Add(customer);
                db.SaveChanges();
                return RedirectToAction("Index");
            }

            return View(customer);
        }

        // GET: Customers/Edit/5
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Customer customer = db.Customers.Find(id);
            if (customer == null)
            {
                return HttpNotFound();
            }
            return View(customer);
        }

        // POST: Customers/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details see https://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "CustomerId,Name,Email,Phone")] Customer customer)
        {
            if (ModelState.IsValid)
            {
                db.Entry(customer).State = EntityState.Modified;
                db.SaveChanges();
                return RedirectToAction("Index");
            }
            return View(customer);
        }

        // GET: Customers/Delete/5
        public async Task<ActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            var customer = await db.Customers
                .Include(c => c.Reservations)
                .Include(c => c.Orders)
                .FirstOrDefaultAsync(c => c.CustomerId == id);

            if (customer == null)
            {
                return HttpNotFound();
            }

            // Check for active reservations or orders
            if (customer.Reservations.Any(r => r.Status == "Pending" || r.Status == "Confirmed") ||
                customer.Orders.Any(o => o.Status == "Pending" || o.Status == "In Progress"))
            {
                TempData["ErrorMessage"] = "Cannot delete customer with active reservations or orders.";
                return RedirectToAction("Index");
            }

            return View(customer);
        }

        // POST: Customers/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteConfirmed(int id)
        {
            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    var customer = await db.Customers
                        .Include(c => c.Reservations)
                        .Include(c => c.Orders)
                        .FirstOrDefaultAsync(c => c.CustomerId == id);

                    if (customer == null)
                    {
                        return HttpNotFound();
                    }

                    // Check again for active items
                    if (customer.Reservations.Any(r => r.Status == "Pending" || r.Status == "Confirmed") ||
                        customer.Orders.Any(o => o.Status == "Pending" || o.Status == "In Progress"))
                    {
                        TempData["ErrorMessage"] = "Cannot delete customer with active reservations or orders.";
                        return RedirectToAction("Index");
                    }

                    // Mark all related records as cancelled/completed
                    foreach (var reservation in customer.Reservations)
                    {
                        reservation.Status = "Cancelled";
                    }

                    foreach (var order in customer.Orders)
                    {
                        order.Status = "Completed";
                    }

                    await db.SaveChangesAsync();

                    // Now safe to delete the customer
                    db.Customers.Remove(customer);
                    await db.SaveChangesAsync();

                    transaction.Commit();
                    return RedirectToAction("Index");
                }
                catch (Exception)
                {
                    transaction.Rollback();
                    TempData["ErrorMessage"] = "An error occurred while deleting the customer.";
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
