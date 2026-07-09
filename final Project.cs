// ----------Encapsulation(التغليف)----------
// : IsBooked لها "private set"، يعني محد يقدر يغيّرها من برا الكلاس مباشرة.
// الطريقة الوحيدة لتغييرها هي عبر دالة Book() اللي فيها التحقق (IsBooked already true?).
// هذا يحمي حالة الكائن من أي تعديل غير متحكم فيه = تغليف صحيح.
class Seat
{
    public string Number { get; }
    public bool IsBooked { get; private set; }

    public Seat(string number) => Number = number;

    public void Book()
    {
        if (IsBooked) throw new InvalidOperationException($"The Seat {Number} Pre-booked.");
        IsBooked = true;
    }
}

abstract class Ticket
{
    public string SeatNumber { get; }
    public decimal BasePrice { get; }
    public abstract string TypeName { get; }

    protected Ticket(string seatNumber, decimal basePrice)
    {
        SeatNumber = seatNumber;
        BasePrice = basePrice;
    }
}

// RegularTicket يرث من Ticket (Inheritance) ويطبق TypeName الخاصة فيه (Polymorphism)
class RegularTicket : Ticket
{
    public RegularTicket(string seat, decimal price) : base(seat, price) { }
    public override string TypeName => "standard";
}

// StudentTicket يرث من نفس الأساس، لكن TypeName ترجع قيمة مختلفة تماماً
// بدون أي شرط if بمكان استخدامها لاحقاً - هذا جوهر تعدد الأشكال
class StudentTicket : Ticket
{
    public StudentTicket(string seat, decimal price) : base(seat, price) { }
    public override string TypeName => "student";
}

// ---------- Factory Method ----------
// لماذا؟ إنشاء نوع التذكرة قد يتوسع لاحقاً (VIP، كبار سن...)
// عزل الإنشاء هنا يحقق OCP: نضيف نوعاً جديداً دون تعديل بقية الكود.

enum TicketKind { Regular, Student }

// TicketFactory هو الكلاس الوحيد اللي "يعرف" كيف يُنشئ كل نوع تذكرة.
// أي كلاس ثاني بالمشروع (زي BookingService) يطلب تذكرة بدون ما يعرف تفاصيل الإنشاء.
static class TicketFactory
{
    public static Ticket Create(TicketKind kind, string seat, decimal price) => kind switch
    {
        TicketKind.Regular => new RegularTicket(seat, price),
        TicketKind.Student => new StudentTicket(seat, price),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };
}

// ---------- Strategy ----------
// لماذا؟ طريقة حساب السعر تتغير (خصم، عرض...)، فعزلها يحقق OCP أيضاً
// دون المساس بمنطق الحجز نفسه.

// ISP: واجهة صغيرة ومحددة الغرض - وظيفة وحدة بس (حساب السعر)
interface IPricingStrategy
{
    decimal CalculatePrice(Ticket ticket);
}

// كل استراتيجية = كلاس منفصل، يقدر تبدلها وقت التشغيل بدون تعديل أي كود قديم
class RegularPricing : IPricingStrategy
{
    public decimal CalculatePrice(Ticket ticket) => ticket.BasePrice;
}

class StudentDiscountPricing : IPricingStrategy
{
    public decimal CalculatePrice(Ticket ticket) => ticket.BasePrice * 0.7m; // خصم 30%
}

// ---------- Observer ----------
// لماذا؟ أكثر من جهة تحتاج تعرف بحدث "تم الحجز" (إيميل، واجهة...)
// بدل ما BookingService يستدعي كل جهة يدوياً.

// ISP أيضاً: واجهة صغيرة، وظيفتها الوحيدة هي "استقبال إشعار"
interface IBookingObserver
{
    void Notify(string seat, string type, decimal price);
}

// أي مراقب جديد (SMS مثلاً) ينضاف كتطبيق جديد لهذي الواجهة، بدون تعديل BookingService
class EmailNotifier : IBookingObserver
{
    public void Notify(string seat, string type, decimal price) =>
        Console.WriteLine($"[Email] Booking Confirmation {seat} ({type}) At a price of {price:C}");
}

// ---------- Singleton ----------
// لماذا؟ حالة المقاعد يجب أن تكون مصدراً وحيداً للحقيقة خلال الجلسة.
// لاحظ أننا لم نستخدمه في أي كلاس آخر لأنه يصعّب الاختبار ولا مبرر له هناك.

sealed class CinemaHall
{
    // Lazy<T> يضمن إنشاء النسخة الوحيدة فقط أول مرة تُستخدم (lazy initialization)
    private static readonly Lazy<CinemaHall> _instance = new(() => new CinemaHall());
    public static CinemaHall Instance => _instance.Value;

    private readonly Dictionary<string, Seat> _seats = new();

    // الـ Constructor خاص (private) لمنع إنشاء أي نسخة ثانية من برا الكلاس
    private CinemaHall() { }

    public void AddSeats(params string[] numbers)
    {
        foreach (var n in numbers) _seats.TryAdd(n, new Seat(n));
    }

    public Seat Get(string number) => _seats[number];
}

// ---------- Service (SRP + DIP) ----------
// SRP: مسؤولية BookingService الوحيدة هي "تنسيق" عملية الحجز، لا الحساب ولا الإشعار ولا الإنشاء.
// DIP: يعتمد على الواجهات فقط (IPricingStrategy، IBookingObserver)، لا على التنفيذ الفعلي.
// أي تنفيذ يُحقن من الخارج عبر الـ Constructor (Dependency Injection).

class BookingService
{
    private readonly IPricingStrategy _pricing;
    private readonly List<IBookingObserver> _observers;

    public BookingService(IPricingStrategy pricing, List<IBookingObserver> observers)
    {
        _pricing = pricing;
        _observers = observers;
    }

    public void Book(TicketKind kind, string seatNumber, decimal basePrice)
    {
        var seat = CinemaHall.Instance.Get(seatNumber);       // Singleton
        var ticket = TicketFactory.Create(kind, seatNumber, basePrice); // Factory Method
        var finalPrice = _pricing.CalculatePrice(ticket);     // Strategy

        seat.Book(); // Encapsulation: التحقق من الحالة يصير داخل Seat نفسها

        foreach (var observer in _observers) // Observer
            observer.Notify(ticket.SeatNumber, ticket.TypeName, finalPrice); // Polymorphism: TypeName تختلف حسب النوع
    }
}

// ---------- Program ----------

class Program
{
    static void Main()
    {
        CinemaHall.Instance.AddSeats("A1", "A2", "B1");
        var observers = new List<IBookingObserver> { new EmailNotifier() };

        Console.WriteLine("-- Book a standard ticket --");
        var regular = new BookingService(new RegularPricing(), observers);
        regular.Book(TicketKind.Regular, "A1", 20m);

        Console.WriteLine("-- Book a discounted student ticket --");
        var student = new BookingService(new StudentDiscountPricing(), observers);
        student.Book(TicketKind.Student, "A2", 20m);

        Console.WriteLine("-- Attempting to reserve a seat (must fail) --");
        try
        {
            // هذا يثبت LSP: أي Ticket فرعي (هنا RegularTicket) يعمل بشكل صحيح
            // مكان أي Ticket أساسي بدون كسر أي سلوك متوقع
            regular.Book(TicketKind.Regular, "A1", 20m);
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine($"Expected error: {ex.Message}");
        }
    }
}









