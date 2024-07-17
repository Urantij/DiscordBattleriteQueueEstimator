﻿// <auto-generated />
using System;
using DiscordBattleriteQueueEstimator.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace DiscordBattleriteQueueEstimator.Migrations
{
    [DbContext(typeof(MyContext))]
    partial class MyContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder.HasAnnotation("ProductVersion", "8.0.7");

            modelBuilder.Entity("DiscordBattleriteQueueEstimator.Data.Models.DbClearPoint", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<long>("Date")
                        .HasColumnType("INTEGER");

                    b.Property<int>("StatusId")
                        .HasColumnType("INTEGER");

                    b.HasKey("Id");

                    b.ToTable("Points");
                });

            modelBuilder.Entity("DiscordBattleriteQueueEstimator.Data.Models.DbUser", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<ulong>("DiscordId")
                        .HasColumnType("INTEGER");

                    b.HasKey("Id");

                    b.ToTable("Users");
                });

            modelBuilder.Entity("DiscordBattleriteQueueEstimator.Data.Models.DbUserStatus", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<long>("Date")
                        .HasColumnType("INTEGER");

                    b.Property<bool>("FakeRp")
                        .HasColumnType("INTEGER");

                    b.Property<int>("UserId")
                        .HasColumnType("INTEGER");

                    b.HasKey("Id");

                    b.HasIndex("UserId");

                    b.ToTable("Statuses");
                });

            modelBuilder.Entity("DiscordBattleriteQueueEstimator.Data.Models.DbUserStatus", b =>
                {
                    b.HasOne("DiscordBattleriteQueueEstimator.Data.Models.DbUser", "User")
                        .WithMany("Statuses")
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.OwnsOne("DiscordBattleriteQueueEstimator.Data.Models.RpInfo", "RpInfo", b1 =>
                        {
                            b1.Property<int>("DbUserStatusId")
                                .HasColumnType("INTEGER");

                            b1.Property<string>("Details")
                                .HasColumnType("TEXT");

                            b1.Property<string>("Hero")
                                .HasColumnType("TEXT");

                            b1.Property<int?>("PartySize")
                                .HasColumnType("INTEGER");

                            b1.Property<string>("State")
                                .IsRequired()
                                .HasColumnType("TEXT");

                            b1.HasKey("DbUserStatusId");

                            b1.ToTable("Statuses");

                            b1.WithOwner()
                                .HasForeignKey("DbUserStatusId");
                        });

                    b.Navigation("RpInfo");

                    b.Navigation("User");
                });

            modelBuilder.Entity("DiscordBattleriteQueueEstimator.Data.Models.DbUser", b =>
                {
                    b.Navigation("Statuses");
                });
#pragma warning restore 612, 618
        }
    }
}
