using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Bookify.Web2.Core;
using System.Linq;
using System.Security.Claims;

namespace Bookify.Web2.Controllers
{
    [Authorize(Roles = AppRoles.Reception)]

    public class RentalsController : Controller
    {

        private readonly IWebHostEnvironment _environment;
        private readonly ApplicationDbContext _context;
        private readonly IMapper _mapper;
        private readonly IDataProtector _dataProtector;

        public RentalsController(ApplicationDbContext context, IMapper mapper, IDataProtectionProvider dataprotector, IWebHostEnvironment environment)
        {
            _context = context;
            _mapper = mapper;
            _environment = environment;
            _dataProtector = dataprotector.CreateProtector("MySecureKey");

        }


        public IActionResult Create(string skey)
        {
            var subscriberId = int.Parse(_dataProtector.Unprotect(skey));
            var subscriber = _context.subscribers
                .Include(s => s.subscribtions)
                .Include(r => r.Rentals)
                .ThenInclude(rc => rc.RentalsCopies)
                .FirstOrDefault(s => s.Id == subscriberId);
            ///////////////////////
            if (subscriber is null)
                return NotFound();
            var (errorMessage, NumberAllowedCopies) = ValidationSubscribers(subscriber);

            if (!string.IsNullOrEmpty(errorMessage))
                return View("NotAllowedRental", errorMessage);

            var viewModel = new RentalFormViewModel
            {
                SubscriberKey = skey,
                MaxAllowedCopy = NumberAllowedCopies

            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]

        public IActionResult Create(RentalFormViewModel model)
        {

            if (!ModelState.IsValid)
                return View(model);
            // Convert subscriber key to subscriber ID
            var subscriberId = int.Parse(_dataProtector.Unprotect(model.SubscriberKey));

            // Retrieve the subscriber
            var subscriber = _context.subscribers
                .Include(s => s.subscribtions)
                .Include(r => r.Rentals)
                .ThenInclude(rc => rc.RentalsCopies)
                .FirstOrDefault(s => s.Id == subscriberId);

            if (subscriber is null)
                return NotFound();

            // Perform any validation checks (e.g., maximum allowed copies, etc.)
            var (errorMessage, NumberAllowedCopies) = ValidationSubscribers(subscriber);

            if (!string.IsNullOrEmpty(errorMessage))
                return View("NotAllowedRental");

            // Retrieve the selected copies based on their serial numbers
            var selectedCopies = _context.bookCopies
                .Include(c => c.Book)
                .Include(c => c.Rentals)
                .Where(c => model.SelectedCopies.Contains(c.SerialNumber))
                .ToList();

            // Retrieve the current subscriber's active rentals
            var currentSubscriberRentals = _context.rentals
                .Include(r => r.RentalsCopies)
                .ThenInclude(c => c.BookCopy)
                .Where(r => r.SubscriberID == subscriberId)
                .SelectMany(r => r.RentalsCopies)
                .Where(c => !c.ReturnedDate.HasValue)
                .Select(c => c.BookCopy!.BookId)
                .ToList();

            List<RentalCopy> copies = new();


            foreach (var copy in selectedCopies)
            {
                // Check if the copy and associated book are available for rental
                if (!copy.IsAvailableForRental || !copy.Book!.IsAvailableForRental)
                    return View("NotAllowedRental", Errors.NotAvailableForRental);

                // Check if the copy is already in another active rental
                if (copy.Rentals.Any(x => !x.ReturnedDate.HasValue))
                    return View("NotAllowedRental", Errors.CopyIsInRental);

                // Check if the subscriber already has the same book
                if (currentSubscriberRentals.Any(BookId => BookId == copy.BookId))
                    return View("NotAllowedRental", $"This subscriber already has borrowed this book: {copy.Book.Title}");

                copies.Add(new RentalCopy { BookCopyID = copy.Id });
            }

            // Create a new rental
            var rental = new Rental
            {
                RentalsCopies = copies,
                CreatedOnByID = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            };

            // Add the rental to the subscriber
            subscriber.Rentals.Add(rental);

            // Save changes to the database
            _context.SaveChanges();

            return Ok();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult GetBookCopiesDetils(SearchFormViewModel model)
        {
            if (!ModelState.IsValid)
                return BadRequest();

            var copy = _context.bookCopies.
                Include(b => b.Book).
                SingleOrDefault(s => s.SerialNumber.ToString() == model.Value && !s.IsDeleted && !s.Book!.IsDeleted);
            if (copy is null)
                return NotFound(Errors.InvalidSerialNumber);
            if (!copy.IsAvailableForRental || !copy.Book!.IsAvailableForRental)
                return BadRequest(Errors.NotAvailableForRental);
            var viewModel = _mapper.Map<BookCopiesModel>(copy);
            return PartialView("_bookCopyDetails", viewModel);
        }


        private (string errorMessage,int? NumberAllowedCopies) ValidationSubscribers(SubscribeR subscriber)
        {
                   
            if (subscriber.IsBlackedList)
                return (errorMessage:Errors.NotAllowedRental,null);
            if (subscriber.subscribtions.Last().EndDate<DateTime.Now.AddDays((int) RentalsConfigurations.RentalDurations))
            return (errorMessage: Errors.InActive, null);

            var allowedRentalCopies = subscriber.Rentals.SelectMany(r => r.RentalsCopies).Count(r => !r.ReturnedDate.HasValue);
            var AvailabeCopiesCount = ((int)RentalsConfigurations.MaxAllowedCopies) - allowedRentalCopies;
            if (AvailabeCopiesCount.Equals(0))
                  return (errorMessage:Errors.MaxCopieshasBeenReached,null);

            return (errorMessage: string.Empty, NumberAllowedCopies: AvailabeCopiesCount);

        }
    }
}
