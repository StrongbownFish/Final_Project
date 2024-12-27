using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using Final_Project.Models;

namespace Final_Project.Controllers
{
    public class PaymentsController : Controller
    {
        private RestaurantContext db = new RestaurantContext();

        // GET: Payments
        public ActionResult Index()
        {
            var orders = db.Orders
                .Include(o => o.Customer)
                .Where(o => o.Status == "Pending")
                .ToList(); 

            return View(orders);
        }


        // GET: Payments/Details/5
        public ActionResult Details(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Payment payment = db.Payments.Find(id);
            if (payment == null)
            {
                return HttpNotFound();
            }
            return View(payment);
        }

        // GET: Payments/Create
        public ActionResult Create(int id)  // id is the OrderId
        {
            var order = db.Orders
                .Include(o => o.Customer)
                .Include(o => o.OrderItems)
                .Include(o => o.OrderItems.Select(oi => oi.MenuItem))
                .FirstOrDefault(o => o.OrderId == id);

            if (order == null)
            {
                return HttpNotFound();
            }

            var payment = new Payment { OrderId = order.OrderId, Order = order };
            return View(payment);
        }

        // POST: Payments/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details see https://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(Payment payment)
        {
            if (ModelState.IsValid)
            {
                using (var transaction = db.Database.BeginTransaction())
                {
                    try
                    {
                        // Set payment time
                        payment.PaymentTime = DateTime.Now;

                        // Get order with OrderItems
                        var order = db.Orders
                            .Include(o => o.Customer)
                            .Include(o => o.OrderItems)
                            .Include(o => o.OrderItems.Select(oi => oi.MenuItem))
                            .FirstOrDefault(o => o.OrderId == payment.OrderId);

                        if (order != null)
                        {
                            payment.Amount = order.TotalAmount;
                            order.Status = "Completed";
                            db.Entry(order).State = EntityState.Modified;
                        }

                        db.Payments.Add(payment);
                        db.SaveChanges();
                        transaction.Commit();

                        return RedirectToAction("Receipt", new { id = payment.PaymentId });
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        ModelState.AddModelError("", "Error processing payment: " + ex.Message);
                    }
                }
            }

            ViewBag.PaymentMethods = new List<string> { "Cash", "Banking", "Visa" };
            return View(payment);
        }
        public ActionResult Receipt(int id)
        {
            var payment = db.Payments
                .Include(p => p.Order)
                .Include(p => p.Order.Customer)
                .Include(p => p.Order.OrderItems)
                .Include(p => p.Order.OrderItems.Select(oi => oi.MenuItem))
                .FirstOrDefault(p => p.PaymentId == id);

            if (payment == null)
            {
                return HttpNotFound();
            }

            return View(payment);
        }

        // GET: Payments/Edit/5
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Payment payment = db.Payments.Find(id);
            if (payment == null)
            {
                return HttpNotFound();
            }
            ViewBag.OrderId = new SelectList(db.Orders, "OrderId", "Status", payment.OrderId);
            return View(payment);
        }

        // POST: Payments/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details see https://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "PaymentId,OrderId,Amount,PaymentTime,PaymentMethod")] Payment payment)
        {
            if (ModelState.IsValid)
            {
                db.Entry(payment).State = EntityState.Modified;
                db.SaveChanges();
                return RedirectToAction("Index");
            }
            ViewBag.OrderId = new SelectList(db.Orders, "OrderId", "Status", payment.OrderId);
            return View(payment);
        }

        // GET: Payments/Delete/5
        public ActionResult Delete(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Payment payment = db.Payments.Find(id);
            if (payment == null)
            {
                return HttpNotFound();
            }
            return View(payment);
        }

        // POST: Payments/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            Payment payment = db.Payments.Find(id);
            db.Payments.Remove(payment);
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
