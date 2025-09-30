using IT15.Data;
using IT15.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace IT15.Services
{
    public class PayrollService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;

        public PayrollService(ApplicationDbContext context, UserManager<IdentityUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<bool> GeneratePayrollForMonth(DateTime month)
        {
            var payrollMonth = new DateTime(month.Year, month.Month, 1);
            if (await _context.Payrolls.AnyAsync(p => p.PayrollMonth == payrollMonth && !p.IsArchived))
            {
                return false;
            }

            var payroll = new Payroll
            {
                PayrollMonth = payrollMonth,
                DateGenerated = DateTime.Now,
                Status = PayrollStatus.PendingApproval
            };

            var employees = await _userManager.GetUsersInRoleAsync("User");

            foreach (var employee in employees)
            {
                decimal basicSalary = 20000;
                int workingDaysInMonth = DateTime.DaysInMonth(month.Year, month.Month);
                decimal dailyRate = workingDaysInMonth > 0 ? basicSalary / workingDaysInMonth : 0;
                decimal hourlyRate = dailyRate / 8;

                // --- Attendance and Leave Calculation (remains the same) ---
                var attendanceCount = await _context.DailyLogs.CountAsync(d => d.UserId == employee.Id && d.CheckInTime.Month == month.Month);
                var approvedLeaves = await _context.LeaveRequests.CountAsync(l => l.RequestingEmployeeId == employee.Id && l.Status == LeaveRequestStatus.Approved && l.StartDate.Month == month.Month);
                int daysPresent = attendanceCount + approvedLeaves;
                int daysAbsent = workingDaysInMonth > daysPresent ? workingDaysInMonth - daysPresent : 0;
                decimal absentDeductions = daysAbsent * dailyRate;

                // --- NEW OVERTIME AND PENALTY LOGIC ---
                decimal totalOvertimePay = 0;
                decimal totalOvertimePenalty = 0;

                var approvedRequests = await _context.OvertimeRequests
                    .Where(r => r.RequestingEmployeeId == employee.Id && r.Status == OvertimeStatus.Approved && r.OvertimeDate.Month == month.Month)
                    .ToListAsync();

                foreach (var request in approvedRequests)
                {
                    var dailyLog = await _context.DailyLogs.FirstOrDefaultAsync(d => d.UserId == employee.Id && d.CheckInTime.Date == request.OvertimeDate.Date);

                    if (dailyLog != null && dailyLog.CheckOutTime.HasValue)
                    {
                        var approvedEndTime = request.OvertimeDate.Date + request.EndTime;

                        // Calculate penalty if checked out early
                        if (dailyLog.CheckOutTime.Value < approvedEndTime)
                        {
                            var unworkedHours = (decimal)(approvedEndTime - dailyLog.CheckOutTime.Value).TotalHours;
                            totalOvertimePenalty += unworkedHours * hourlyRate;
                        }

                        // Calculate pay for actual overtime worked
                        var overtimeStart = request.OvertimeDate.Date.AddHours(18); // Assumes OT starts at 6 PM
                        if (dailyLog.CheckOutTime.Value > overtimeStart)
                        {
                            var actualOvertimeHours = (decimal)(dailyLog.CheckOutTime.Value - overtimeStart).TotalHours;
                            totalOvertimePay += actualOvertimeHours * hourlyRate * 1.25m; // 125% rate
                        }
                    }
                }

                // --- Final Payroll Calculation ---
                decimal sss = 500, philhealth = 300, pagibig = 100;
                decimal grossPay = Math.Max(0, (basicSalary - absentDeductions) + totalOvertimePay);
                decimal totalDeductions = sss + philhealth + pagibig + absentDeductions + totalOvertimePenalty;
                decimal netPay = Math.Max(0, grossPay - totalDeductions);

                var paySlip = new PaySlip
                {
                    EmployeeId = employee.Id,
                    DaysAbsent = daysAbsent,
                    BasicSalary = basicSalary,
                    OvertimePay = totalOvertimePay,
                    OvertimePenalty = totalOvertimePenalty,
                    AbsentDeductions = absentDeductions,
                    SSSDeduction = sss,
                    PhilHealthDeduction = philhealth,
                    PagIBIGDeduction = pagibig,
                    TaxDeduction = 10,
                    GrossPay = grossPay,
                    TotalDeductions = totalDeductions,
                    NetPay = netPay
                };
                payroll.PaySlips.Add(paySlip);
            }

            _context.Payrolls.Add(payroll);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}