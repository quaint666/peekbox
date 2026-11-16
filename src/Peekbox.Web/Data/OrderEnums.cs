namespace Peekbox.Web.Data;

// The five statuses of an order (decision 2026-11-14). The customer's strip shows the first four as steps; Cancelled
// replaces the strip with a notice. Cancelled is possible from New, Preparing or Delivering. Stored as text.
public enum OrderStatus { New, Preparing, Delivering, Delivered, Cancelled }

// F44. ZaloPay is a stand-in until it is decided (decision 2026-11-11): the order is marked paid at once.
public enum PaymentMethod { ZaloPay, CashOnDelivery }

// Kept apart from OrderStatus, as in Bloomy: paying and delivering move on their own.
public enum PaymentStatus { Pending, Paid, Failed, Refunded }

// F42's time window. The times are in Services/DeliveryWindows.cs.
public enum DeliveryWindow { Morning, Afternoon, Evening }
