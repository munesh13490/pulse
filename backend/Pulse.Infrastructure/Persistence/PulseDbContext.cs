using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Pulse.Infrastructure.Persistence;

public sealed class PulseDbContext(DbContextOptions<PulseDbContext> options) : DbContext(options);
